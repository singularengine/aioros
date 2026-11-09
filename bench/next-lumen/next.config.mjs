// LUMEN_CACHE=1 builds and serves the cached variant (Cache Components: "use cache" + cacheTag, updateTag on save) into
// .next-cache; unset, the default variant renders every request into .next. Both are measured by bench/app.sh.
// The variant picks its root layout (layout.cache.jsx or layout.live.jsx) by page extension and its page bodies
// (lib/views/cached.jsx or lib/views/live.js) by alias; the views themselves are shared.
const cached = process.env.LUMEN_CACHE === "1";

export default {
  poweredByHeader: false,
  distDir: cached ? ".next-cache" : ".next",
  cacheComponents: cached,
  pageExtensions: [cached ? "cache.jsx" : "live.jsx", "jsx", "js"],
  turbopack: { resolveAlias: { "lumen-views": cached ? "./lib/views/cached.jsx" : "./lib/views/live.js" } },
};
