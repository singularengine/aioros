import { icons } from "../lib/data";

export default function Icon({ kind, size = 16, stroke = 2, color }) {
  return (
    <svg className="icon" viewBox="0 0 24 24" width={size} height={size} fill="none" stroke="currentColor" strokeWidth={stroke}
         strokeLinecap="round" strokeLinejoin="round" style={color ? { color } : undefined}
         dangerouslySetInnerHTML={{ __html: icons[kind] ?? "" }} />
  );
}
