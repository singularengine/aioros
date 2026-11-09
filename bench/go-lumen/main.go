// Lumen in Go (bench/app.sh variant `go`): net/http with Go 1.22's pattern routing, templ components compiled to Go at
// build time, htmx's hx-boost for partial navigation and form posts (the "GOTH" stack: Go, templ, htmx), gzip
// compression (klauspost/compress/gzhttp) and Go 1.25's cross-origin protection (Fetch Metadata) on posts.
// Data: bench/lumen-shared/fixtures.json, read at start; issues are mutable in memory, one immutable snapshot per save.
// usage: go-lumen -addr 127.0.0.1:5237 -shared ../lumen-shared
package main

import (
	"bytes"
	"encoding/json"
	"flag"
	"fmt"
	"log"
	"net/http"
	"os"
	"path/filepath"
	"slices"
	"strings"
	"sync/atomic"
	"time"

	"github.com/a-h/templ"
	"github.com/klauspost/compress/gzhttp"
)

type Status struct{ Key, Name, Color string }
type Person struct{ Name, Initials, Color string }
type Label struct{ Name, Color string }
type Priority struct{ Key, Name string }

type Issue struct {
	ID, Title, Description, Status, Priority, Assignee string
	Labels                                             []string
	Team, Updated                                      string
}

type Notification struct {
	Icon, Color, Who, Text, Issue, At string
	Unread                            bool
}

type fixtures struct {
	Statuses      []Status
	Priorities    json.RawMessage
	People        []Person
	Issues        []Issue
	Notifications []Notification
	Labels        []Label
	Icons         map[string]string
}

var (
	statuses      []Status
	statusByKey   = map[string]Status{}
	priorities    []Priority
	people        []Person
	personByName  = map[string]Person{}
	labelByName   = map[string]Label{}
	icons         map[string]string
	notifications []Notification
	issues        atomic.Pointer[[]Issue]
	cssHref       string
	css           []byte
)

func load(dir string) {
	raw, err := os.ReadFile(filepath.Join(dir, "fixtures.json"))
	if err != nil {
		log.Fatal(err)
	}
	var f fixtures
	if err := json.Unmarshal(raw, &f); err != nil {
		log.Fatal(err)
	}
	statuses, people, icons, notifications = f.Statuses, f.People, f.Icons, f.Notifications
	for _, s := range statuses {
		statusByKey[s.Key] = s
	}
	for _, p := range people {
		personByName[p.Name] = p
	}
	for _, l := range f.Labels {
		labelByName[l.Name] = l
	}
	dec := json.NewDecoder(bytes.NewReader(f.Priorities))
	dec.Token()
	for dec.More() {
		k, _ := dec.Token()
		v, _ := dec.Token()
		priorities = append(priorities, Priority{k.(string), v.(string)})
	}
	all := slices.Clone(f.Issues)
	issues.Store(&all)
	shared, err := os.ReadFile(filepath.Join(dir, "lumen.css"))
	if err != nil {
		log.Fatal(err)
	}
	static, err := os.ReadFile(filepath.Join(dir, "static-ssr.css"))
	if err != nil {
		log.Fatal(err)
	}
	css = append(append(shared, '\n'), static...)
	cssHref = fmt.Sprintf("/lumen.css?v=%x", len(css))
}

// Row is an issue with what its views show, computed per request as the other variants do.
type Row struct {
	Issue
	Href, StatusIcon, StatusColor, PriorityIcon, PriorityColor, UpdatedText string
	Person                                                                  Person
	LabelList                                                               []Label
}

func decorate(i Issue) Row {
	r := Row{Issue: i, Href: "/issues/" + i.ID, StatusIcon: "status-" + i.Status, StatusColor: statusByKey[i.Status].Color,
		PriorityIcon: "prio-" + i.Priority, PriorityColor: "#8B8F98", Person: personByName[i.Assignee], UpdatedText: ago(i.Updated)}
	if i.Priority == "urgent" {
		r.PriorityColor = "#F2994A"
	}
	for _, n := range i.Labels {
		r.LabelList = append(r.LabelList, labelByName[n])
	}
	return r
}

func allRows() []Row {
	all := *issues.Load()
	rows := make([]Row, len(all))
	for k, i := range all {
		rows[k] = decorate(i)
	}
	return rows
}

func find(id string) (Row, bool) {
	for _, i := range *issues.Load() {
		if i.ID == id {
			return decorate(i), true
		}
	}
	return Row{}, false
}

func update(id string, f func(*Issue)) {
	for {
		old := issues.Load()
		next := slices.Clone(*old)
		for k := range next {
			if next[k].ID == id {
				f(&next[k])
				next[k].Updated = time.Now().Format("2006-01-02T15:04:05")
			}
		}
		if issues.CompareAndSwap(old, &next) {
			return
		}
	}
}

var now = time.Date(2026, 10, 10, 12, 0, 0, 0, time.Local)

func ago(t string) string {
	at, err := time.ParseInLocation("2006-01-02T15:04:05", t, time.Local)
	if err != nil {
		return ""
	}
	m := max(0, int(now.Sub(at).Minutes()))
	switch {
	case m < 60:
		return fmt.Sprintf("%dm ago", m)
	case m < 1440:
		return fmt.Sprintf("%dh ago", m/60)
	default:
		return fmt.Sprintf("%dd ago", m/1440)
	}
}

// Page is what the layout needs from the request.
type Page struct {
	Path  string
	Dark  bool
	Title string
}

func page(r *http.Request, title string) Page {
	c, err := r.Cookie("theme")
	return Page{Path: r.URL.Path, Dark: err == nil && c.Value == "dark", Title: title}
}

func (p Page) Theme() string {
	if p.Dark {
		return "dark"
	}
	return "light"
}

func render(w http.ResponseWriter, r *http.Request, c templ.Component) {
	templ.Handler(c).ServeHTTP(w, r)
}

type issueForm struct{ Error, Saved string }

func main() {
	addr := flag.String("addr", "127.0.0.1:5237", "listen address")
	dir := flag.String("shared", "../lumen-shared", "bench/lumen-shared")
	assets := flag.String("assets", "node_modules", "htmx")
	flag.Parse()
	load(*dir)
	feed := startFeed()

	mux := http.NewServeMux()
	mux.HandleFunc("GET /{$}", func(w http.ResponseWriter, r *http.Request) { http.Redirect(w, r, "/issues", http.StatusFound) })
	mux.HandleFunc("GET /issues", func(w http.ResponseWriter, r *http.Request) {
		render(w, r, IssuesPage(page(r, "Issues · Lumen"), allRows()))
	})
	issue := func(w http.ResponseWriter, r *http.Request, form issueForm) {
		i, ok := find(r.PathValue("id"))
		if !ok {
			w.WriteHeader(http.StatusNotFound)
			render(w, r, NotFoundPage(page(r, "Not found")))
			return
		}
		render(w, r, IssuePage(page(r, i.ID+" · "+i.Title), i, form))
	}
	mux.HandleFunc("GET /issues/{id}", func(w http.ResponseWriter, r *http.Request) { issue(w, r, issueForm{}) })
	mux.HandleFunc("POST /issues/{id}", func(w http.ResponseWriter, r *http.Request) {
		title := strings.TrimSpace(r.PostFormValue("title"))
		if title == "" {
			issue(w, r, issueForm{Error: "Title is required."})
			return
		}
		update(r.PathValue("id"), func(i *Issue) {
			i.Title, i.Description = title, r.PostFormValue("description")
			i.Status, i.Priority, i.Assignee = r.PostFormValue("status"), r.PostFormValue("priority"), r.PostFormValue("assignee")
		})
		issue(w, r, issueForm{Saved: "Changes saved."})
	})
	mux.HandleFunc("GET /projects", func(w http.ResponseWriter, r *http.Request) {
		render(w, r, ProjectsPage(page(r, "Projects · Lumen"), allRows()))
	})
	mux.HandleFunc("GET /inbox", func(w http.ResponseWriter, r *http.Request) {
		render(w, r, InboxPage(page(r, "Inbox · Lumen"), allRows()))
	})
	mux.HandleFunc("GET /settings", func(w http.ResponseWriter, r *http.Request) {
		render(w, r, SettingsPage(page(r, "Settings · Lumen"), settingsForm{Name: "Edsger Dijkstra", Email: "edsger@lumen.dev", Weekly: true}))
	})
	mux.HandleFunc("POST /settings", func(w http.ResponseWriter, r *http.Request) {
		f := settingsForm{Name: r.PostFormValue("name"), Email: r.PostFormValue("email"), Weekly: r.PostFormValue("weekly") != "", Desktop: r.PostFormValue("desktop") != ""}
		switch {
		case !strings.Contains(f.Email, "@"):
			f.Error = "The email is not valid."
		case strings.TrimSpace(f.Name) == "":
			f.Error = "Name is required."
		default:
			f.Saved = "Changes saved."
		}
		render(w, r, SettingsPage(page(r, "Settings · Lumen"), f))
	})
	mux.HandleFunc("GET /dashboard", func(w http.ResponseWriter, r *http.Request) {
		render(w, r, DashboardPage(page(r, "Dashboard · Lumen"), feed.current.Load().snapshot))
	})
	mux.HandleFunc("GET /theme/{name}", func(w http.ResponseWriter, r *http.Request) {
		next := r.PathValue("name")
		if next == "toggle" {
			next = map[bool]string{true: "light", false: "dark"}[page(r, "").Dark]
		} else if next != "dark" {
			next = "light"
		}
		http.SetCookie(w, &http.Cookie{Name: "theme", Value: next, Path: "/", MaxAge: 31536000, SameSite: http.SameSiteLaxMode})
		back := "/"
		if u, err := r.URL.Parse(r.Referer()); err == nil && r.Referer() != "" {
			back = u.Path
		}
		http.Redirect(w, r, back, http.StatusFound)
	})
	immutable := func(file, kind string) http.HandlerFunc {
		return func(w http.ResponseWriter, r *http.Request) {
			w.Header().Set("Cache-Control", "public, max-age=31536000, immutable")
			w.Header().Set("Content-Type", kind)
			http.ServeFile(w, r, file)
		}
	}
	mux.HandleFunc("GET /htmx.min.js", immutable(filepath.Join(*assets, "htmx.org/dist/htmx.min.js"), "text/javascript"))
	mux.HandleFunc("GET /sse.min.js", immutable(filepath.Join(*assets, "htmx-ext-sse/dist/sse.min.js"), "text/javascript"))
	mux.HandleFunc("GET /lumen.css", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Cache-Control", "public, max-age=31536000, immutable")
		w.Header().Set("Content-Type", "text/css; charset=utf-8")
		w.Write(css)
	})

	handler := http.NewServeMux()
	handler.HandleFunc("GET /dashboard/live", feed.serve)
	handler.Handle("/", gzhttp.GzipHandler(http.NewCrossOriginProtection().Handler(mux)))
	log.Printf("listening on http://%s", *addr)
	log.Fatal(http.ListenAndServe(*addr, handler))
}
