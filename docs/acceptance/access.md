# Page access

Pages and whole route segments that only signed-in users may open. The rule is written once, as ASP.NET Core's `[Authorize]` on the page's or a layout's loader, or as a folder convention in the composition (`app.MapAioros(pages => pages.AuthorizeFolder("/account"))`), and is enforced by ASP.NET Core's authorization on the page's endpoints, so the app's authentication scheme decides what an anonymous visitor gets (the cookie scheme of ASP.NET Core Identity redirects to its login path with `ReturnUrl`). View models stay free of it (principle 15); `[Authorize]` on a view model is still read, deprecated, until step 1.9. Precedents: Razor Pages (`[Authorize]` on a `PageModel`, `AuthorizeFolder`, `AuthorizePage`, `AllowAnonymousToPage` conventions), Next.js and React Router (a check in a parent layout or loader protects every child route).

6 criteria. Each has an identifier and exactly one test.

## Open model

### ACCESS-01: Access rules come from the layout and page models

- **Given** a page whose view model or one of whose layout models has `[Authorize]` (with an optional policy and roles)
- **When** the project is built
- **Then**
  - Its page definition lists one access rule per attribute, from the outermost layout to the page; a page with `[AllowAnonymous]` on its view model lists none; a page without either lists none.
  - (Deprecated fallback until step 1.9.)

### ACCESS-04: Access rules come from the loaders

- **Given** `[Authorize]` (with an optional policy and roles) on a layout's loader (`Layout.Loader.cs`) and on a page's loader (`Page.Loader.cs`), whose view models carry no attribute
- **When** the project is built
- **Then** the page's definition lists one access rule per attribute, the layout's first, so ACCESS-02 challenges an anonymous visitor and forbids a user without the policy; within one segment the loader's rules come before its view model's; a page whose loader has `[AllowAnonymous]` is marked as allowing anonymous visitors and lists no rule, also under a protected layout.

## Access

### ACCESS-02: Pages with access rules challenge and forbid

- **Given** a page with an access rule (from a loader, a view model or a folder convention) and cookie authentication
- **When** an anonymous visitor requests it, posts to it or opens its live stream
- **Then** the answer is the scheme's challenge (a redirect to the login path with `ReturnUrl`); a signed-in user without the rule's policy is forbidden; a signed-in user with it gets the page; a page without rules is served to anonymous visitors.

### ACCESS-03: The signed in user reaches the loader

- **Given** a signed-in user
- **When** a page loads
- **Then** `PageRequest.User` is that user's principal; for an anonymous visitor it is an unauthenticated principal.

### ACCESS-05: Folder conventions protect pages without loaders

- **Given** pages without loaders and the conventions `AuthorizeFolder("/account", "owners")`, `AuthorizePage("/reports")` and `AllowAnonymousToPage("/account/login")` (folders and pages named by their folder under `Pages`, matched without case, a folder covering every page below it)
- **When** the endpoints are mapped and the pages are requested
- **Then** an anonymous visitor to `/account/settings` or `/reports` gets the challenge, a signed-in user without the `owners` policy is forbidden from `/account/settings`, one with it gets the page, `/account/login` and a page outside the folders are served to anonymous visitors, and a page whose definition allows anonymous visitors is served under a protected folder; the convention's rules come before the page's own.

*Precedent: Razor Pages `AuthorizeFolder`, `AuthorizePage` and `AllowAnonymousToPage`.*

### ACCESS-06: A protected pages template keeps its access rules

- **Given** a reusable page with access rules (signed in, role `owner`) and a public page, each with its plan cached under a fingerprint of its own
- **When** `/_aioros/plan/{fingerprint}` is asked for the protected page's template
- **Then** an anonymous visitor gets `401` and a signed-in visitor the rules refuse `403` (the codes `/_data` answers, never the scheme's redirect, since the script fetches it), and the owner gets the template with `Cache-Control: private, max-age=31536000, immutable`; the public page's template, and a template a public page shares with a protected one, is answered to anyone with `public, max-age=31536000, immutable`, as before.

*Precedent: `/_data`, which checks the page's access rules itself; Next.js, whose route's RSC payload passes the same middleware as its page.*
