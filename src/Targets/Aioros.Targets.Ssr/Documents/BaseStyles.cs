namespace Aioros.Targets.Ssr.Documents;

internal static class BaseStyles
{
    public const string Css = """
        *,*::before,*::after{box-sizing:border-box}
        body{margin:0;font-family:system-ui,-apple-system,"Segoe UI",Roboto,sans-serif}
        h1,h2{margin:0;font-size:inherit;font-weight:inherit}
        button,input,textarea,select{font-family:inherit;font-size:inherit;font-weight:inherit;font-style:inherit;margin:0;border-style:solid}
        button{cursor:pointer;background:none;color:inherit}
        input,textarea{width:100%;min-width:0;outline:none}
        a{text-decoration:none;cursor:pointer;color:inherit}
        .c{display:flex;flex-direction:column;min-width:0}
        :root{interpolate-size:allow-keywords}
        details>summary{list-style:none;cursor:pointer;display:flex;align-items:center;gap:6px;user-select:none}
        details>summary::-webkit-details-marker{display:none}
        details>summary>.chev{transition:transform .2s ease;flex-shrink:0;opacity:.55}
        details[open]>summary>.chev{transform:rotate(90deg)}
        details::details-content{block-size:0;overflow:hidden;transition:block-size .25s cubic-bezier(.33,1,.68,1),content-visibility .25s allow-discrete}
        details[open]::details-content{block-size:auto}
        [popover].menu{margin:0;position:fixed;inset:auto;overflow:visible}:where([popover].menu){padding:0;border:0 solid;background:none;color:inherit}
        [popover].menu:popover-open{display:flex;flex-direction:column}
        .aioros-draft{position:fixed;right:16px;bottom:16px;z-index:2147483646;display:flex;gap:8px;align-items:center;padding:10px 12px;border:1px solid GrayText;border-radius:8px;background:Canvas;color:CanvasText;box-shadow:0 4px 16px rgba(0,0,0,.2);font-family:system-ui,sans-serif;font-size:14px}
        .aioros-draft button{padding:4px 10px;border:1px solid GrayText;border-radius:6px;background:Canvas;color:CanvasText}
        @supports not (anchor-name:--a){[popover].menu{inset:56px 16px auto auto}}
        :where(.menu .mi){display:flex;align-items:center;gap:10px;padding:7px 10px;border-radius:6px;font-size:13px;border:0;background:none;color:inherit;text-align:start;width:100%}
        :where(.menu .mi>.mh){flex:1;min-width:0}:where(.menu .kg){font-family:inherit}
        :where(.menu hr){border:0;border-top:1px solid currentColor;opacity:.12;margin:4px 0}
        [data-drag]{cursor:grab;user-select:none}[data-dragging]{opacity:.55;cursor:grabbing}[data-drop][aria-busy]{cursor:progress}
        [data-aioros-follow=capture]{touch-action:none}
        .aioros-live{position:fixed;width:1px;height:1px;overflow:hidden;clip-path:inset(50%);white-space:nowrap}
        @supports (-moz-appearance:none){:root{scrollbar-color:light-dark(#0006,#fff6) transparent}}

        """;
}
