package main

// The live dashboard's simulated feed, the same algorithm, seed and update rules as bench/next-lumen/app/dashboard/feed.js.
// Each tick renders the dashboard once into a Server-Sent Events message that htmx's SSE extension swaps in. Subscribers
// wait on the current tick's channel, closed when the next tick is published.

import (
	"bytes"
	"context"
	"fmt"
	"math"
	"net/http"
	"os"
	"strconv"
	"strings"
	"sync/atomic"
	"time"
)

type Kpi struct {
	Name, Value, Delta string
	Up                 bool
}

type Bar struct {
	Label, Value string
	Height       float64
}

type Order struct{ ID, Customer, Amount, Status string }

type Snapshot struct {
	Kpis                           []Kpi
	Line, Area, TrafficNow, Updated string
	Bars                           []Bar
	Orders                         []Order
}

type tick struct {
	snapshot Snapshot
	message  []byte
	next     chan struct{}
}

type Feed struct{ current atomic.Pointer[tick] }

const points, cw, ch, barMax = 60, 600.0, 180.0, 130.0

var customers = []string{"Ada Lovelace", "Alan Turing", "Grace Hopper", "Linus Torvalds", "Margaret Hamilton", "Dennis Ritchie",
	"Barbara Liskov", "Ken Thompson", "Frances Allen", "Donald Knuth", "Radia Perlman", "John McCarthy"}

type simulation struct {
	a                                                           uint32
	traffic, sales                                              []float64
	orders                                                      []Order
	revenue, prevRevenue, orderCount, prevOrders, prevConv      float64
	nextOrder                                                   int
}

func (s *simulation) rnd() float64 {
	s.a += 0x6D2B79F5
	a := s.a
	t := (a ^ a>>15) * (1 | a)
	t = (t + (t^t>>7)*(61|t)) ^ t
	return float64(t^t>>14) / 4294967296
}

func (s *simulation) next(lo, hi int) int { return lo + int(math.Floor(s.rnd()*float64(hi-lo))) }

func (s *simulation) newOrder() Order {
	r := s.rnd()
	status := "failed"
	if r < 0.7 {
		status = "paid"
	} else if r < 0.9 {
		status = "pending"
	}
	amount := 20 + float64(s.next(0, 480)) + float64(s.next(0, 100))/100
	id := "#" + strconv.Itoa(s.nextOrder)
	s.nextOrder++
	return Order{ID: id, Customer: customers[s.next(0, len(customers))], Amount: "$" + strconv.FormatFloat(amount, 'f', 2, 64), Status: status}
}

func n0(v float64) string {
	digits := strconv.FormatInt(int64(math.Round(v)), 10)
	var b strings.Builder
	for i, c := range digits {
		if i > 0 && (len(digits)-i)%3 == 0 {
			b.WriteByte(',')
		}
		b.WriteRune(c)
	}
	return b.String()
}

func pct(now, before float64) string {
	p := 0.0
	if before != 0 {
		p = (now - before) / before * 100
	}
	sign := ""
	if p > 0 {
		sign = "+"
	}
	return sign + strconv.FormatFloat(p, 'f', 2, 64) + "%"
}

func short(v float64) string {
	s := strconv.FormatFloat(v, 'f', 2, 64)
	return strings.TrimRight(strings.TrimRight(s, "0"), ".")
}

func newSimulation() *simulation {
	s := &simulation{a: 42, revenue: 128430, prevRevenue: 128430, orderCount: 1204, prevOrders: 1204, nextOrder: 10240, prevConv: 3.2}
	for i := range points {
		s.traffic = append(s.traffic, 520+120*math.Sin(float64(i)/6)+float64(s.next(-40, 40)))
	}
	for range 12 {
		s.sales = append(s.sales, 30+float64(s.next(0, 70)))
	}
	for range 8 {
		s.orders = append(s.orders, s.newOrder())
	}
	return s
}

func (s *simulation) tick() Snapshot {
	copy(s.traffic, s.traffic[1:])
	s.traffic[points-1] = min(950, max(150, s.traffic[points-2]+float64(s.next(-60, 61))))
	h := s.next(0, 12)
	s.sales[h] = min(100, max(5, s.sales[h]+float64(s.next(-15, 16))))
	if s.rnd() < 0.4 {
		o := s.newOrder()
		s.orders = append([]Order{o}, s.orders...)
		if len(s.orders) > 8 {
			s.orders = s.orders[:8]
		}
		s.orderCount++
		amount, _ := strconv.ParseFloat(o.Amount[1:], 64)
		s.revenue += amount
	}
	conv := math.Round(min(6, max(1.5, s.prevConv+(s.rnd()-0.5)*0.2))*100) / 100
	t, tPrev := s.traffic[points-1], s.traffic[points-2]
	kpis := []Kpi{
		{"Revenue", "$" + n0(s.revenue), pct(s.revenue, s.prevRevenue), s.revenue >= s.prevRevenue},
		{"Orders", n0(s.orderCount), pct(s.orderCount, s.prevOrders), s.orderCount >= s.prevOrders},
		{"Active users", n0(t), pct(t, tPrev), t >= tPrev},
		{"Conversion", strconv.FormatFloat(conv, 'f', 2, 64) + "%", pct(conv, s.prevConv), conv >= s.prevConv},
	}
	s.prevRevenue, s.prevOrders, s.prevConv = s.revenue, s.orderCount, conv
	parts := make([]string, points)
	for i, v := range s.traffic {
		parts[i] = short(float64(i)*cw/(points-1)) + "," + short(ch-v/1000*(ch-10))
	}
	line := strings.Join(parts, " ")
	bars := make([]Bar, len(s.sales))
	for i, v := range s.sales {
		bars[i] = Bar{Label: fmt.Sprintf("%02dh", 8+i), Height: math.Round(v/100*barMax*10) / 10, Value: strconv.FormatFloat(v*0.42, 'f', 1, 64) + "k"}
	}
	return Snapshot{Kpis: kpis, Line: line, Area: fmt.Sprintf("%s %v,%v 0,%v", line, cw, ch, ch), TrafficNow: n0(t) + " /min",
		Bars: bars, Orders: append([]Order(nil), s.orders...), Updated: "Updated " + time.Now().Format("15:04:05.000")}
}

func publish(f *Feed, s Snapshot) {
	var html bytes.Buffer
	Dash(s).Render(context.Background(), &html)
	var msg bytes.Buffer
	for line := range strings.SplitSeq(html.String(), "\n") {
		msg.WriteString("data: " + line + "\n")
	}
	msg.WriteString("\n")
	t := &tick{snapshot: s, message: msg.Bytes(), next: make(chan struct{})}
	if old := f.current.Swap(t); old != nil {
		close(old.next)
	}
}

func startFeed() *Feed {
	f := &Feed{}
	sim := newSimulation()
	publish(f, sim.tick())
	every := time.Second
	if ms, err := strconv.Atoi(os.Getenv("POC_TICK_MS")); err == nil {
		every = time.Duration(ms) * time.Millisecond
	}
	go func() {
		for range time.Tick(every) {
			publish(f, sim.tick())
		}
	}()
	return f
}

func (f *Feed) serve(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "text/event-stream")
	w.Header().Set("Cache-Control", "no-cache, no-transform")
	flusher := w.(http.Flusher)
	for t := f.current.Load(); ; {
		if _, err := w.Write(t.message); err != nil {
			return
		}
		flusher.Flush()
		select {
		case <-t.next:
			t = f.current.Load()
		case <-r.Context().Done():
			return
		}
	}
}
