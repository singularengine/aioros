# Installed app sign-in

The installed app working against a remote server (`Aioros:Server`, `AiorosRole.Client`) signs in through the system browser and calls `/_data` and `/_sf` with a bearer token, while the website keeps its cookies. The server side is an OAuth 2.0 authorization server for the app's own native client, built on OpenIddict (one public client, PKCE S256, a loopback redirect with any port, rotating refresh tokens) over the app's existing cookie sign-in; the client side is a small PKCE client with a loopback listener, a token store per operating system and a message handler on the clients of `/_data` and `/_sf`. Both come from one call, `builder.AddInstalledAppSignIn(...)`. Precedents: RFC 8252 (OAuth for native apps: the system browser, loopback redirects with any port), RFC 7636 (PKCE), RFC 9700 (refresh token rotation and reuse detection), OpenIddict's samples, IdentityModel.OidcClient's loopback sample, VS Code and GitHub Desktop, Uno's `HeaderHandler` and `MauiBlazorWebIdentity` (a handler that attaches and refreshes the token).

19 criteria. Each has an identifier and exactly one test.

## Installed app authorization

### SIGNIN-01: Codes are exchanged only with their verifier

- **Given** the authorization server over a cookie sign-in, a user signed in with the cookie and an authorization request for the app's client with a PKCE S256 challenge and a loopback redirect
- **When** the browser asks `/connect/authorize`
- **Then** it is redirected to the loopback address with a `code` and the request's `state`
- **When** the app posts that code to `/connect/token` with the matching `code_verifier`
- **Then** it gets a bearer access token that expires in the configured lifetime and a refresh token
- **When** it posts a code with a wrong verifier
- **Then** the answer is `400` with `invalid_grant` and no token.

### SIGNIN-02: Only loopback redirects with a challenge get a code

- **Given** the authorization server
- **When** the authorization request names a loopback redirect `http://127.0.0.1:{port}/callback`
- **Then** any port is accepted (RFC 8252, section 7.3)
- **When** it names another host, another path or no PKCE challenge
- **Then** no code is issued and the browser is not redirected there
- **When** the visitor has no cookie
- **Then** the answer is the cookie scheme's challenge, a redirect to the app's sign-in route with the authorization request as `ReturnUrl`.

### SIGNIN-03: Refresh tokens rotate and a reused one is refused

- **Given** a refresh token issued by SIGNIN-01
- **When** the app redeems it at `/connect/token`
- **Then** it gets a new access token and a new refresh token (rotation)
- **When** the old refresh token is redeemed again
- **Then** the answer is `400` with `invalid_grant` (RFC 9700 reuse detection).

### SIGNIN-04: Bearer tokens are accepted on data and functions alongside the cookie

- **Given** a page protected by `[Authorize]` and a server function protected by `[Authorize]`
- **When** `/_data` and `/_sf` are called with `Authorization: Bearer` and an access token from SIGNIN-01
- **Then** both answer `200` for the user the token was issued to, whose principal carries the cookie's user id and name (`ClaimTypes.NameIdentifier`, `Identity.Name`); without a token, or with a forged one, they answer `401`; with the sign-in cookie they still answer `200`.

### SIGNIN-06: The client signs in through the browser and stores the tokens

- **Given** the sign-in client of the installed app and the authorization server
- **When** it signs in
- **Then** it opens the system browser on `/connect/authorize` with `response_type=code`, its client id, an S256 challenge, a random `state`, its loopback redirect and `device_name` (the machine's name and its operating system, `ana-laptop (Linux)`), and no `return_path` request outside the first run (INSTALL-17), and once the browser (here an HTTP client holding the sign-in cookie, following the redirect) reaches the loopback, it exchanges the code with its verifier and writes the access token, the refresh token and the expiry to the token store.

## Loopback redirect

### SIGNIN-05: The redirect listens on a loopback port and checks the state

- **Given** the loopback redirect
- **When** it starts
- **Then** it listens on `127.0.0.1` on a port the operating system chose and its redirect is `http://127.0.0.1:{port}/callback`
- **When** the browser arrives with the expected `state` and a `code`
- **Then** it answers a page that says the window can be closed and returns the code with the answer's other parameters (such as `return_path`, INSTALL-16); a request to another path is answered `404` and ignored; a `state` that does not match, or an `error`, fails the sign-in and answers a page that says so.

## Signed in handler

### SIGNIN-07: Tokens are attached refreshed and retried once

- **Given** the sign-in client's handler on the HTTP client of `/_data` and `/_sf`
- **When** a request is sent with stored tokens
- **Then** it carries `Authorization: Bearer {access token}`
- **When** the access token expires within a minute (a fake `TimeProvider`)
- **Then** it is refreshed before the request is sent
- **When** the server answers `401`
- **Then** the handler refreshes once and sends the request again with the new token
- **When** the refresh is refused or the second answer is also `401`
- **Then** the `401` is returned, and `/_data` and the generated function clients fail with `SignInRequiredException` (an `UnauthorizedAccessException`); concurrent requests that need a refresh redeem the refresh token once.

## Desktop sign in

### SIGNIN-08: A remaining unauthorized signs in through the browser

- **Given** the desktop navigator of the installed app with the sign-in client
- **When** a navigation fails because the server still answers `401`
- **Then** the navigator goes to the app's sign-in route with the page as `ReturnUrl`: it shows the built-in "Continue in your browser" view, runs the browser sign-in and, once signed in, shows the page it was going to
- **When** the sign-in fails or is cancelled
- **Then** `Failed` is raised and the error page is shown; a navigation to the sign-in route itself clears the stored tokens and runs the same browser sign-in.

### SIGNIN-17: The sign out route revokes the tokens and offers to sign in again

- **Given** the desktop navigator of the installed app signed in against the server
- **When** it navigates to the sign-out route (`/logout`)
- **Then** the refresh token is revoked at the server (it no longer refreshes), the token store is cleared and the window shows the built-in "You are signed out" view with a "Sign in" button
- **When** that button is clicked
- **Then** the browser sign-in runs (SIGNIN-08) and the window shows the home page.

## Token store

### SIGNIN-09: Each store keeps the tokens in its systems form

- **Given** the libsecret store (`secret-tool`)
- **When** tokens are written, read and cleared
- **Then** it runs `secret-tool store --label=... service aioros app {app} server {origin}` with the tokens on standard input (never on the command line), `secret-tool lookup` with the same attributes and `secret-tool clear` with them; a lookup that finds nothing reads as no tokens
- **When** the Keychain store
- **Then** it runs `security add-generic-password -U -s aioros:{app} -a {origin} -w {tokens}`, `find-generic-password -s -a -w` and `delete-generic-password -s -a`
- **When** the DPAPI store
- **Then** the file it writes holds the tokens protected for the current user, never in clear, and reads them back.

### SIGNIN-10: Libsecret keeps the tokens on this machine

- **Given** this machine has `secret-tool` and a session bus
- **When** the libsecret store writes tokens, reads them back and clears them
- **Then** the tokens read are the tokens written and nothing is found after clearing (skipped where libsecret is not available).

### SIGNIN-11: The store is the operating systems or a reported session store

- **Given** the operating system
- **When** the token store is chosen
- **Then** Windows gets DPAPI, macOS the Keychain and Linux libsecret when `secret-tool` is on the path; otherwise the tokens are kept in memory for the session and a warning says so: never a file in clear and never silently.

## Installed app host

### SIGNIN-12: The sign in is composed for the role of the host

- **Given** the app composed with `builder.AddInstalledAppSignIn(...)`
- **When** it runs as the website (`AiorosRole.Server`)
- **Then** the authorization server, its client registration and the bearer validation are registered (and `MapAioros` maps `/connect/authorize`, which SIGNIN-01 exercises); as the installed client (`AiorosRole.Client`), then the sign-in client is registered and its handler is on the clients of `/_data` and every server function group; in process (`AiorosRole.InProcess`), then neither, and `InstalledAppSessions` is registered on the website and in process (where it lists nothing), so a page that lists the sessions loads in every role.

## Installed app sessions

### SIGNIN-13: A revoked refresh token ends its session

- **Given** tokens issued by SIGNIN-01
- **When** the app posts its refresh token to `/connect/revoke` with its client id and `token_type_hint=refresh_token` (RFC 7009)
- **Then** the answer is `200`, the refresh token can no longer refresh (`400` with `invalid_grant`) and the access token issued with it is refused on `/_data` (`401`) before it expires, since the session it belongs to is revoked; revoking a token the server does not know also answers `200` (RFC 7009, section 2.2).

### SIGNIN-15: The users sessions are listed and revoked one by one

- **Given** a user signed in on two installed apps whose authorization requests named the devices `ana-laptop (Linux)` and `ana-desk (Windows)`, and another user signed in on a third
- **When** the user's installed-app sessions are listed (`InstalledAppSessions.ListAsync`)
- **Then** they are the user's two sessions, newest first, each with its id, its device name and when it was created and last used, and none of the other user's
- **When** the user revokes one of them
- **Then** the answer is `true`, its refresh token can no longer refresh and its access token is refused, while the other session still refreshes; revoking the other user's session or an unknown id answers `false` and changes nothing; a device name longer than 64 characters is cut to 64 and control characters are dropped.

### SIGNIN-16: Signing out on the web revokes installed apps only when asked

- **Given** a user signed in on the website with the cookie and in the installed app
- **When** the user signs out on the website (the cookie scheme's sign-out)
- **Then** by default the installed app's session stays valid and its refresh token still refreshes, as signing out of a browser on GitHub or Google signs out only that browser; with `RevokeOnWebSignOut` set, then the same sign-out revokes every installed-app session of that user, and another user's session stays valid.

## Sign out

### SIGNIN-14: Signing out revokes the refresh token and clears the store

- **Given** the sign-in client with stored tokens
- **When** it signs out
- **Then** it posts the refresh token to the revocation endpoint with `token_type_hint=refresh_token` and its client id, clears the token store, answers `true`, and afterwards has no access token to send
- **When** the server cannot be reached or does not answer `200`
- **Then** the store is still cleared and it answers `false`; without stored tokens it posts nothing.

## Token keys

### SIGNIN-27: The server needs no rsa key and issues only data protection tokens

- **Given** the authorization server registered by `AddInstalledAppAuthorization`
- **When** its options are resolved
- **Then** its credentials are keys made in microseconds when the server is registered (an ECDSA P-256 signing key, the asymmetric key OpenIddict requires, and an AES-256 key wrap), never an RSA key generated on the first request, and with no setting to write
- **When** a user signs in
- **Then** the authorization code, the access token and the refresh token it issues are ASP.NET Core Data Protection tokens, none of them a JWT, so no token depends on those credentials.

### SIGNIN-28: Tokens outlive a restart with the same key ring

- **Given** a server that issued tokens to the installed app
- **When** it restarts (a second server over the same store and the same data protection key ring, the first one stopped)
- **Then** the access token is accepted on `/_data` (`200`) and the refresh token redeems (`200`, rotated)
- **When** the second server has another key ring
- **Then** the same access token is refused (`401`) and the refresh token answers `400` with `invalid_grant`: the tokens live as long as the app's data protection keys, as the sign-in cookie does.
