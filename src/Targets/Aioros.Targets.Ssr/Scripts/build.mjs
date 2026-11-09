import { build } from "esbuild";
import { readdir } from "node:fs/promises";
import { join } from "node:path";

const [out] = process.argv.slice(2);
const sources = new URL("./src/", import.meta.url).pathname;
const modules = (await readdir(sources)).filter((name) => name.endsWith(".js"));

await build({
  entryPoints: modules.map((name) => join(sources, name)),
  outdir: out,
  bundle: true,
  minify: true,
  format: "esm",
  target: "es2022",
  legalComments: "none",
  logLevel: "warning",
});

await build({
  entryPoints: [join(sources, "app", "index.js")],
  outfile: join(out, "app.js"),
  bundle: true,
  minify: true,
  format: "iife",
  target: "es2022",
  legalComments: "none",
  logLevel: "warning",
});
