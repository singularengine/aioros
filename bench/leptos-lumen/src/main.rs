// The Axum server of the Leptos build: Leptos's routes and server functions, the theme switch, the dashboard's
// Server-Sent Events, and response compression (Brotli and gzip, tower-http), as the other variants compress.
#[cfg(feature = "ssr")]
#[tokio::main]
async fn main() {
    use axum::Router;
    use axum::routing::get;
    use leptos::prelude::*;
    use leptos_axum::{LeptosRoutes, generate_route_list};
    use leptos_lumen::app::{App, shell};
    use tower_http::compression::CompressionLayer;

    let conf = get_configuration(None).expect("leptos configuration");
    let options = conf.leptos_options;
    let addr = options.site_addr;
    let routes = generate_route_list(App);
    leptos_lumen::feed::start();

    let app = Router::new()
        .route("/", get(|| async { (axum::http::StatusCode::FOUND, [(axum::http::header::LOCATION, "/issues")]) }))
        .route("/theme/{name}", get(theme))
        .route("/dashboard/live", get(live))
        .leptos_routes(&options, routes, {
            let options = options.clone();
            move || shell(options.clone())
        })
        .fallback(leptos_axum::file_and_error_handler(shell))
        .layer(CompressionLayer::new())
        .with_state(options);

    let listener = tokio::net::TcpListener::bind(&addr).await.expect("bind");
    axum::serve(listener, app.into_make_service()).await.expect("serve");
}

#[cfg(feature = "ssr")]
async fn theme(
    axum::extract::Path(name): axum::extract::Path<String>,
    headers: axum::http::HeaderMap,
) -> impl axum::response::IntoResponse {
    use axum::http::{StatusCode, header};
    let dark = leptos_lumen::app::cookie_dark(headers.get(header::COOKIE).and_then(|v| v.to_str().ok()));
    let next = match name.as_str() {
        "toggle" => if dark { "light" } else { "dark" },
        "dark" => "dark",
        _ => "light",
    };
    let back = headers
        .get(header::REFERER)
        .and_then(|v| v.to_str().ok())
        .and_then(|r| r.split_once("://"))
        .and_then(|(_, rest)| rest.find('/').map(|i| rest[i..].to_string()))
        .unwrap_or_else(|| "/".into());
    (
        StatusCode::FOUND,
        [(header::SET_COOKIE, format!("theme={next}; Path=/; Max-Age=31536000; SameSite=Lax")), (header::LOCATION, back)],
    )
}

#[cfg(feature = "ssr")]
async fn live() -> axum::response::Sse<impl tokio_stream::Stream<Item = Result<axum::response::sse::Event, std::convert::Infallible>>> {
    use tokio_stream::StreamExt;
    let rx = leptos_lumen::feed::live().snapshot.clone();
    let stream = tokio_stream::wrappers::WatchStream::new(rx)
        .map(|(_, message)| Ok(axum::response::sse::Event::default().data(&*message)));
    axum::response::Sse::new(stream)
}

#[cfg(not(feature = "ssr"))]
pub fn main() {}
