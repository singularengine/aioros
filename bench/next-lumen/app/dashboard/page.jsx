import { connection } from "next/server";
import { Suspense } from "react";
import TopBar from "../TopBar";
import Dashboard from "./Dashboard";
import { getFeed } from "./feed";

export const metadata = { title: "Dashboard · Lumen" };

// The live feed is request-time data: connection() keeps it from being prerendered with a snapshot taken at build time,
// and Suspense lets the rest of the page be prerendered around it
// (https://nextjs.org/docs/app/getting-started/caching#streaming-uncached-data).
async function Live() {
  await connection();
  return <Dashboard initial={getFeed().snapshot} />;
}

export default function Page() {
  return (<><TopBar title="Dashboard" icon="dashboard" /><Suspense fallback={null}><Live /></Suspense></>);
}
