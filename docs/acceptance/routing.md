# Routing

Route templates and route tables (`src/Aioros.Routing`).

Precedents: segment precedence and conflict rules from [matchit](https://github.com/ibraheemdev/matchit#conflict-rules) (the router behind Axum) and ASP.NET Core's route precedence; React Router ranks by a comparable score (static 10, dynamic 3, splat penalty). Constraint syntax (`{id:int}`) and catch-all syntax (`{*path}`) follow ASP.NET Core and matchit.

File-based routes follow the [Next.js App Router conventions](https://nextjs.org/docs/app/getting-started/project-structure): a folder is a segment, `Page.axaml` makes it routable, `Layout.axaml` wraps every page below it, `[name]` is a parameter, `[...name]` and `[[...name]]` are catch-alls, `(group)` folders organize without changing the URL, and `_private` folders are never routed. `[name:constraint]` is Aioros's addition for typed parameters.

21 criteria. Each has an identifier and exactly one test.

## Route table

### ROUTING-01: A literal path matches its route

- **Given** a table with `/issues`
- **When** `/issues` is matched
- **Then** it matches the template `/issues` with no values.

### ROUTING-02: Parameters are captured and unescaped

- **Given** a table with `/issues/{id}`
- **When** `/issues/LUM%2D105` is matched
- **Then** `id` is `LUM-105`, unescaped.

### ROUTING-03: Several parameters are captured

- **Given** a template with two parameters
- **When** a path fills both
- **Then** both values are captured by name.

### ROUTING-04: Literals match without case and trailing slashes

- **Given** a literal template
- **When** the path differs only in letter case or a trailing slash
- **Then** it still matches.

### ROUTING-05: An unknown path matches nothing

- **Given** a table without a matching template
- **When** a path is matched
- **Then** the result is no match with an empty template.

## Route precedence

### ROUTING-06: Literals win over parameters regardless of order

- **Given** `/issues/{id}` added before `/issues/new`
- **When** `/issues/new` is matched
- **Then** the literal template wins, regardless of the order the templates were added (matchit, ASP.NET Core precedence).

### ROUTING-07: Constraints filter parameter values

- **Given** `/issues/{id:int}`
- **When** `/issues/42` and `/issues/abc` are matched
- **Then** the first matches with `id` = `42` and the second does not match.

### ROUTING-08: Constrained parameters win over plain parameters

- **Given** `/issues/{key}` added before `/issues/{id:int}`
- **When** `/issues/42` is matched
- **Then** the constrained template wins, and `/issues/abc` matches `/issues/{key}`.

### ROUTING-09: Catch alls capture the rest and lose to literals

- **Given** `/docs/{*path}` and `/docs/intro`
- **When** `/docs/guide/routing` and `/docs/intro` are matched
- **Then** the first captures `path` = `guide/routing` and the second matches the literal template.

### ROUTING-10: Equivalent templates conflict

- **Given** a table with `/issues/{id}`
- **When** `/issues/{key}` is added
- **Then** it is rejected as a conflict that names the existing template (matchit).

### ROUTING-11: Catch alls must be last

- **Given** a template with a catch-all that is not the last segment
- **When** it is parsed
- **Then** it is rejected.

## File routes

### ROUTING-12: Folders become segments

- **Given** `Pages/Page.axaml` and `Pages/issues/Page.axaml`
- **When** they are mapped to routes
- **Then** they are `/` and `/issues`.

### ROUTING-13: Bracket folders become parameters

- **Given** the folders `[id]` and `[id:int]`
- **When** a page inside them is mapped
- **Then** the segments are `{id}` and `{id:int}`.

### ROUTING-14: Spread folders become catch alls

- **Given** the folders `[...slug]` and `[[...slug]]`
- **When** a page inside them is mapped
- **Then** the segment is the catch-all `{*slug}`.

### ROUTING-15: Groups are left out of the route

- **Given** a route group folder such as `(marketing)`
- **When** a page inside it is mapped
- **Then** the group does not appear in the route.

### ROUTING-16: Private folders and other files are not routes

- **Given** a file inside a private folder such as `_components`, or a file not named `Page.axaml`
- **When** it is mapped
- **Then** it is not a route.

### ROUTING-17: Layouts wrap pages from the outside

- **Given** `Layout.axaml` files in `Pages/` and `Pages/(app)/`
- **When** the page `Pages/(app)/issues/[id]/Page.axaml` is mapped
- **Then** its layouts are those two, outermost first, and layouts in other folders are not included.

### ROUTING-18: Pages resolving to the same route conflict

- **Given** two pages that resolve to the same route, such as `Pages/(a)/about/Page.axaml` and `Pages/(b)/about/Page.axaml`
- **When** they are mapped
- **Then** the result reports a conflict naming both files.

### ROUTING-19: Backslashes are separators

- **Given** paths with backslashes
- **When** they are mapped
- **Then** they give the same routes and sources as with forward slashes.

### ROUTING-20: Not found pages are not routes

- **Given** `Pages/NotFound.axaml` and layouts in `Pages/`
- **When** the files are mapped
- **Then** it is not a route but the not-found page, with the layouts of its folder, as `not-found.tsx` in Next.js.

### ROUTING-21: Error pages are not routes

- **Given** `Pages/Error.axaml`
- **When** the files are mapped
- **Then** it is not a route but the error page, with the layouts of its folder, as `error.tsx` in Next.js.
