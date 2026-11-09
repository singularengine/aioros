// The live dashboard's simulated feed, the same algorithm, seed and update rules as bench/next-lumen/app/dashboard/feed.js.
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
pub struct Kpi {
    pub name: String,
    pub value: String,
    pub delta: String,
    pub up: bool,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
pub struct Bar {
    pub label: String,
    pub height: f64,
    pub value: String,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
pub struct Order {
    pub id: String,
    pub customer: String,
    pub amount: String,
    pub status: String,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
pub struct Snapshot {
    pub kpis: Vec<Kpi>,
    pub line: String,
    pub area: String,
    #[serde(rename = "trafficNow")]
    pub traffic_now: String,
    pub bars: Vec<Bar>,
    pub orders: Vec<Order>,
    pub updated: String,
}

#[cfg(feature = "ssr")]
mod server {
    use super::*;
    use std::sync::{Arc, OnceLock};
    use tokio::sync::watch;

    const POINTS: usize = 60;
    const CW: f64 = 600.0;
    const CH: f64 = 180.0;
    const BAR_MAX: f64 = 130.0;
    const CUSTOMERS: [&str; 12] = ["Ada Lovelace", "Alan Turing", "Grace Hopper", "Linus Torvalds", "Margaret Hamilton",
        "Dennis Ritchie", "Barbara Liskov", "Ken Thompson", "Frances Allen", "Donald Knuth", "Radia Perlman", "John McCarthy"];

    struct Feed {
        a: u32,
        traffic: Vec<f64>,
        sales: Vec<f64>,
        orders: Vec<Order>,
        revenue: f64,
        prev_revenue: f64,
        order_count: f64,
        prev_orders: f64,
        next_order: u32,
        prev_conv: f64,
    }

    fn n0(v: f64) -> String {
        let s = format!("{}", v.round() as i64);
        let (sign, digits) = s.strip_prefix('-').map_or(("", s.as_str()), |d| ("-", d));
        let mut out = String::new();
        for (i, c) in digits.chars().enumerate() {
            if i > 0 && (digits.len() - i) % 3 == 0 { out.push(','); }
            out.push(c);
        }
        format!("{sign}{out}")
    }

    fn pct(now: f64, before: f64) -> String {
        let p = if before == 0.0 { 0.0 } else { (now - before) / before * 100.0 };
        format!("{}{p:.2}%", if p > 0.0 { "+" } else { "" })
    }

    fn short(v: f64) -> String {
        let s = format!("{v:.2}");
        s.trim_end_matches('0').trim_end_matches('.').to_string()
    }

    impl Feed {
        fn rnd(&mut self) -> f64 {
            self.a = self.a.wrapping_add(0x6D2B79F5);
            let a = self.a;
            let mut t = (a ^ (a >> 15)).wrapping_mul(1 | a);
            t = t.wrapping_add((t ^ (t >> 7)).wrapping_mul(61 | t)) ^ t;
            f64::from(t ^ (t >> 14)) / 4294967296.0
        }
        fn next(&mut self, lo: i64, hi: i64) -> i64 {
            lo + (self.rnd() * (hi - lo) as f64).floor() as i64
        }
        fn new_order(&mut self) -> Order {
            let r = self.rnd();
            let status = if r < 0.7 { "paid" } else if r < 0.9 { "pending" } else { "failed" };
            let amount = 20.0 + self.next(0, 480) as f64 + self.next(0, 100) as f64 / 100.0;
            let id = format!("#{}", self.next_order);
            self.next_order += 1;
            let customer = CUSTOMERS[self.next(0, CUSTOMERS.len() as i64) as usize].to_string();
            Order { id, customer, amount: format!("${amount:.2}"), status: status.into() }
        }
        fn create() -> Feed {
            let mut f = Feed { a: 42, traffic: vec![], sales: vec![], orders: vec![], revenue: 128430.0, prev_revenue: 128430.0,
                order_count: 1204.0, prev_orders: 1204.0, next_order: 10240, prev_conv: 3.2 };
            for i in 0..POINTS {
                let v = 520.0 + 120.0 * (i as f64 / 6.0).sin() + f.next(-40, 40) as f64;
                f.traffic.push(v);
            }
            for _ in 0..12 {
                let v = 30.0 + f.next(0, 70) as f64;
                f.sales.push(v);
            }
            for _ in 0..8 {
                let o = f.new_order();
                f.orders.push(o);
            }
            f
        }
        fn tick(&mut self) -> Snapshot {
            self.traffic.rotate_left(1);
            let step = self.next(-60, 61) as f64;
            self.traffic[POINTS - 1] = (self.traffic[POINTS - 2] + step).clamp(150.0, 950.0);
            let h = self.next(0, 12) as usize;
            let step = self.next(-15, 16) as f64;
            self.sales[h] = (self.sales[h] + step).clamp(5.0, 100.0);
            if self.rnd() < 0.4 {
                let o = self.new_order();
                self.revenue += o.amount[1..].parse::<f64>().unwrap_or(0.0);
                self.orders.insert(0, o);
                self.orders.truncate(8);
                self.order_count += 1.0;
            }
            let conv = ((self.prev_conv + (self.rnd() - 0.5) * 0.2).clamp(1.5, 6.0) * 100.0).round() / 100.0;
            let (t, t_prev) = (self.traffic[POINTS - 1], self.traffic[POINTS - 2]);
            let kpis = vec![
                Kpi { name: "Revenue".into(), value: format!("${}", n0(self.revenue)), delta: pct(self.revenue, self.prev_revenue), up: self.revenue >= self.prev_revenue },
                Kpi { name: "Orders".into(), value: n0(self.order_count), delta: pct(self.order_count, self.prev_orders), up: self.order_count >= self.prev_orders },
                Kpi { name: "Active users".into(), value: n0(t), delta: pct(t, t_prev), up: t >= t_prev },
                Kpi { name: "Conversion".into(), value: format!("{conv:.2}%"), delta: pct(conv, self.prev_conv), up: conv >= self.prev_conv },
            ];
            self.prev_revenue = self.revenue;
            self.prev_orders = self.order_count;
            self.prev_conv = conv;
            let line = self.traffic.iter().enumerate()
                .map(|(i, v)| format!("{},{}", short(i as f64 * CW / (POINTS - 1) as f64), short(CH - v / 1000.0 * (CH - 10.0))))
                .collect::<Vec<_>>().join(" ");
            let bars = self.sales.iter().enumerate().map(|(i, v)| Bar {
                label: format!("{:02}h", 8 + i), height: (v / 100.0 * BAR_MAX * 10.0).round() / 10.0, value: format!("{:.1}k", v * 0.42),
            }).collect();
            let ms = std::time::SystemTime::now().duration_since(std::time::UNIX_EPOCH).map(|d| d.as_millis()).unwrap_or(0);
            let s = (ms / 1000) % 86400;
            Snapshot {
                kpis, area: format!("{line} {CW},{CH} 0,{CH}"), line, traffic_now: format!("{} /min", n0(t)), bars,
                orders: self.orders.clone(),
                updated: format!("Updated {:02}:{:02}:{:02}.{:03}", s / 3600, s % 3600 / 60, s % 60, ms % 1000),
            }
        }
    }

    pub struct Live {
        pub snapshot: watch::Receiver<(Arc<Snapshot>, Arc<str>)>,
    }

    static LIVE: OnceLock<Live> = OnceLock::new();

    /// Starts the ticker (one snapshot and one encoded message per tick, shared by every subscriber).
    pub fn start() {
        let mut feed = Feed::create();
        let first = feed.tick();
        let encode = |s: &Snapshot| -> Arc<str> { serde_json::to_string(s).unwrap_or_default().into() };
        let (tx, rx) = watch::channel((Arc::new(first.clone()), encode(&first)));
        let _ = LIVE.set(Live { snapshot: rx });
        let ms = std::env::var("POC_TICK_MS").ok().and_then(|v| v.parse().ok()).unwrap_or(1000);
        tokio::spawn(async move {
            let mut every = tokio::time::interval(std::time::Duration::from_millis(ms));
            every.tick().await;
            loop {
                every.tick().await;
                let s = feed.tick();
                let m = encode(&s);
                if tx.send((Arc::new(s), m)).is_err() { break; }
            }
        });
    }

    pub fn live() -> &'static Live {
        LIVE.get().expect("feed started")
    }
}

#[cfg(feature = "ssr")]
pub use server::{live, start};
