// `next start` on every server core: one Next.js production server per worker of Node's cluster module, which shares
// the listening port between them. A single Node process renders on one core; the Next.js self-hosting guide scales
// out with more instances behind a load balancer, and the cluster module is Node's built-in way to do that on one
// host (https://nodejs.org/api/cluster.html, https://nextjs.org/docs/app/guides/custom-server).
// usage: NEXT_WORKERS=8 node cluster.mjs <port> <host>   (LUMEN_CACHE=1 serves the cached build, see next.config.mjs)
// Caveat for the cached variant: each worker has its own in-memory cache, so a save in one worker does not expire the
// others; a multi-instance deployment needs a shared cache handler. The benchmark only reads.
import cluster from "node:cluster";
import http from "node:http";
import next from "next";

const port = +process.argv[2], hostname = process.argv[3] ?? "127.0.0.1";
const workers = +(process.env.NEXT_WORKERS ?? 1);

if (cluster.isPrimary && workers > 1) {
  for (let i = 0; i < workers; i++) cluster.fork();
} else {
  const app = next({ dev: false, hostname, port });
  await app.prepare();
  const handle = app.getRequestHandler();
  http.createServer((req, res) => handle(req, res)).listen(port, hostname);
}
