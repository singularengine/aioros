"use cache";
// LUMEN_CACHE=1: Cache Components (https://nextjs.org/docs/app/getting-started/caching). Each page body is a cached
// component tagged with the data it reads; saveIssue expires the "issues" tag with updateTag (read-your-own-writes).
import { cacheLife, cacheTag } from "next/cache";
import IssuesView from "../../app/issues/View";
import IssueView from "../../app/issues/[id]/View";
import ProjectsView from "../../app/projects/View";
import InboxView from "../../app/inbox/View";
import SettingsView from "../../app/settings/View";

export async function Issues() {
  cacheLife("max"); cacheTag("issues");
  return <IssuesView />;
}

export async function Issue({ id }) {
  cacheLife("max"); cacheTag("issues", "issue-" + id);
  return <IssueView id={id} />;
}

export async function Projects() {
  cacheLife("max"); cacheTag("issues");
  return <ProjectsView />;
}

export async function Inbox() {
  cacheLife("max"); cacheTag("issues");
  return <InboxView />;
}

export async function Settings() {
  cacheLife("max");
  return <SettingsView />;
}
