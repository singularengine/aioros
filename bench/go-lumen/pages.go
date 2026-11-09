package main

import (
	"slices"
	"strings"
)

type settingsForm struct {
	Name, Email     string
	Weekly, Desktop bool
	Error, Saved    string
}

type group struct {
	Status Status
	Rows   []Row
}

func activeCount(rows []Row) int {
	n := 0
	for _, r := range rows {
		if r.Status == "progress" || r.Status == "review" {
			n++
		}
	}
	return n
}

func byStatus(rows []Row, key string) []Row {
	var out []Row
	for _, r := range rows {
		if r.Status == key {
			out = append(out, r)
		}
	}
	return out
}

// groups: the issues by status in the fixtures' order, most recently updated first, empty groups left out.
func groups(rows []Row) []group {
	var out []group
	for _, s := range statuses {
		g := byStatus(rows, s.Key)
		if len(g) == 0 {
			continue
		}
		slices.SortStableFunc(g, func(a, b Row) int { return strings.Compare(b.Updated, a.Updated) })
		out = append(out, group{s, g})
	}
	return out
}

// columns: the board's five columns, cards by id.
func columns(rows []Row) []group {
	var out []group
	for _, key := range []string{"backlog", "todo", "progress", "review", "done"} {
		g := byStatus(rows, key)
		slices.SortStableFunc(g, func(a, b Row) int { return strings.Compare(a.ID, b.ID) })
		out = append(out, group{statusByKey[key], g})
	}
	return out
}

type inboxItem struct {
	Notification
	Person Person
	Issue  Row
	Time   string
}

func unreadCount() int {
	n := 0
	for _, x := range notifications {
		if x.Unread {
			n++
		}
	}
	return n
}

func inbox(rows []Row) []inboxItem {
	out := make([]inboxItem, 0, len(notifications))
	for _, n := range notifications {
		i := slices.IndexFunc(rows, func(r Row) bool { return r.ID == n.Issue })
		if i < 0 {
			continue
		}
		out = append(out, inboxItem{Notification: n, Person: personByName[n.Who], Issue: rows[i], Time: ago(n.At)})
	}
	return out
}

func orderLabel(status string) string {
	switch status {
	case "paid":
		return "Paid"
	case "pending":
		return "Pending"
	}
	return "Failed"
}
