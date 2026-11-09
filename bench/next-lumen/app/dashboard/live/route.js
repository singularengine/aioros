import { getFeed } from "../feed";


// Server-Sent Events: the latest snapshot as JSON on every tick.
export async function GET(req) {
  const feed = getFeed();
  let unsubscribe = () => {};
  const stream = new ReadableStream({
    start(controller) {
      controller.enqueue(feed.message);
      unsubscribe = feed.subscribe(m => { try { controller.enqueue(m); } catch { unsubscribe(); } });
      req.signal.addEventListener("abort", () => { unsubscribe(); try { controller.close(); } catch {} });
    },
    cancel() { unsubscribe(); },
  });
  return new Response(stream, { headers: { "Content-Type": "text/event-stream", "Cache-Control": "no-cache, no-transform" } });
}
