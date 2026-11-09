// The root layout of the cached variant (LUMEN_CACHE=1). It reads no cookie, so every page is prerendered whole into its
// static shell and served from the cache. Reading the theme cookie here would make every segment under it a blocking,
// request-time render; Next.js's guidance for a theme kept in a cookie is to prerender a default and let an inline script
// in <head> set data-theme from document.cookie before the first paint
// (https://nextjs.org/docs/app/guides/preventing-flash-before-hydration#storing-the-theme-in-a-cookie).
import Shell from "./Shell";
import "./lumen.css";

export const metadata = { title: "Lumen" };

const theme = `(function(){try{var m=document.cookie.match(/(?:^|; )theme=([^;]*)/);if(m&&m[1]==="dark")document.documentElement.setAttribute("data-theme","dark")}catch(e){}})()`;

export default function RootLayout({ children }) {
  return (
    <html lang="en" data-theme="light" suppressHydrationWarning>
      <head><script dangerouslySetInnerHTML={{ __html: theme }} /></head>
      <body><Shell>{children}</Shell></body>
    </html>
  );
}
