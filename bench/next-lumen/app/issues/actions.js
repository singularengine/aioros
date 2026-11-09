"use server";
import { updateTag } from "next/cache";
import { updateIssue } from "../../lib/data";

export async function saveIssue(id, prev, formData) {
  const title = String(formData.get("title") ?? "").trim();
  if (!title) return { error: "Title is required.", saved: "" };
  updateIssue(id, {
    title, description: String(formData.get("description") ?? ""), status: formData.get("status"),
    priority: formData.get("priority"), assignee: formData.get("assignee"),
  });
  updateTag("issues");
  return { error: "", saved: "Changes saved." };
}
