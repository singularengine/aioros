import { notFound } from "next/navigation";
import { Issue } from "lumen-views";
import { allIssues, findIssue } from "../../../lib/data";

// Every issue is known at build time, so each detail page is prerendered (and in the cached variant served from cache).
export const generateStaticParams = () => allIssues().map(i => ({ id: i.id }));

export async function generateMetadata({ params }) {
  const issue = findIssue((await params).id);
  return { title: issue ? `${issue.id} · ${issue.title}` : "Not found" };
}

export default async function IssuePage({ params }) {
  const { id } = await params;
  if (!findIssue(id)) notFound();
  return <Issue id={id} />;
}
