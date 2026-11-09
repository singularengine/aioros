import { cookies } from "next/headers";
import { NextResponse } from "next/server";

export async function GET(req, { params }) {
  const { name } = await params;
  const store = await cookies();
  const current = store.get("theme")?.value === "dark" ? "dark" : "light";
  const next = name === "toggle" ? (current === "dark" ? "light" : "dark") : name === "dark" ? "dark" : "light";
  const back = req.headers.get("referer");
  const res = NextResponse.redirect(new URL(back ? new URL(back).pathname : "/", req.url));
  res.cookies.set("theme", next, { path: "/", maxAge: 31536000, sameSite: "lax" });
  return res;
}
