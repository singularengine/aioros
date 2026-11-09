"use server";
export async function saveSettings(prev, formData) {
  const name = String(formData.get("name") ?? ""), email = String(formData.get("email") ?? "");
  const error = !email.includes("@") ? "The email is not valid." : !name.trim() ? "Name is required." : "";
  return { error, saved: error ? "" : "Changes saved." };
}
