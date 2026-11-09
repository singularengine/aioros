import { cookies } from "next/headers";
import Shell from "./Shell";
import "./lumen.css";

export const metadata = { title: "Lumen" };

export default async function RootLayout({ children }) {
  const theme = (await cookies()).get("theme")?.value === "dark" ? "dark" : "light";
  return (
    <html lang="en" data-theme={theme}>
      <body><Shell theme={theme}>{children}</Shell></body>
    </html>
  );
}
