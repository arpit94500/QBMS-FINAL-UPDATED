/*
 * QBMS shared math module (Setter create/edit, Vetter review, read-only views)
 * ---------------------------------------------------------------------------
 * - One popup (#questionMathModal) for every editor: Math buttons are matched by
 *   .editor-math-btn, .vetter-math-btn, .question-math-btn or #questionMathBtn.
 * - Equations are stored in the database as plain LaTeX text: \( ... \) / \[ ... \].
 * - Inside an editor an equation is a non-editable <span class="latex-math-inline"
 *   data-latex="..."> that MathJax renders. QBMSMath.serialize(editor) turns it back
 *   into \( ... \) so MathJax's generated markup is never saved.
 * - QBMSMath.hydrate(root) turns stored \( ... \) text (and the older
 *   .offline-math-node markup) back into rendered equations. It is called for every
 *   editor on load, and for read-only content marked with .qbms-math-content.
 * - If MathJax is not available a small built-in renderer is used so the popup still works.
 */
(function () {
    "use strict";

    var INLINE_RE = /\\\(([\s\S]+?)\\\)/g;
    var DISPLAY_RE = /\\\[([\s\S]+?)\\\]/g;

    function mj() {
        return window.MathJax && typeof window.MathJax.typesetPromise === "function"
            ? window.MathJax : null;
    }

    function esc(text) {
        var d = document.createElement("div");
        d.textContent = text;
        return d.innerHTML;
    }

    /* ---------- small fallback renderer (used only when MathJax is missing) ---------- */
    function fallbackRender(latex) {
        var r = esc(latex);
        r = r.replace(/\\frac\s*\{([^{}]+)\}\s*\{([^{}]+)\}/g, "($1)/($2)");
        r = r.replace(/\\sqrt\s*\{([^{}]+)\}/g, "&radic;($1)");
        r = r.replace(/\^\{([^{}]+)\}/g, "<sup>$1</sup>").replace(/\^([A-Za-z0-9])/g, "<sup>$1</sup>");
        r = r.replace(/_\{([^{}]+)\}/g, "<sub>$1</sub>").replace(/_([A-Za-z0-9])/g, "<sub>$1</sub>");
        var map = { alpha: "α", beta: "β", gamma: "γ", delta: "δ", theta: "θ", lambda: "λ", mu: "μ", pi: "π",
            sigma: "σ", omega: "ω", times: "×", div: "÷", pm: "±", leq: "≤", geq: "≥", neq: "≠", infty: "∞",
            sum: "∑", int: "∫", cdot: "·", degree: "°" };
        r = r.replace(/\\([A-Za-z]+)/g, function (m, n) { return map[n] || n; });
        return r;
    }

    /* ---------- equation nodes ---------- */
    function createNode(latex, isDisplay) {
        var node = document.createElement(isDisplay ? "div" : "span");
        node.className = isDisplay ? "latex-math-display" : "latex-math-inline";
        node.setAttribute("contenteditable", "false");
        node.setAttribute("data-latex", latex);
        node.setAttribute("data-latex-mode", isDisplay ? "display" : "inline");
        node.textContent = isDisplay ? "\\[" + latex + "\\]" : "\\(" + latex + "\\)";
        return node;
    }

    function typeset(nodes) {
        var m = mj();
        if (!m) {
            nodes.forEach(function (n) {
                if (n.getAttribute && n.getAttribute("data-latex") && !n.getAttribute("data-fallback")) {
                    n.innerHTML = fallbackRender(n.getAttribute("data-latex"));
                    n.setAttribute("data-fallback", "1");
                }
            });
            return Promise.resolve();
        }
        if (m.startup && m.startup.promise) {
            return m.startup.promise.then(function () { return m.typesetPromise(nodes); })
                .catch(function (e) { if (window.console) console.error("MathJax:", e); });
        }
        return m.typesetPromise(nodes).catch(function (e) { if (window.console) console.error("MathJax:", e); });
    }

    function textNodesWithMath(root) {
        var out = [];
        var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null, false);
        var n;
        while ((n = walker.nextNode())) {
            if (n.nodeValue.indexOf("\\(") > -1 || n.nodeValue.indexOf("\\[") > -1) {
                if (!n.parentNode.closest || !n.parentNode.closest(".latex-math-inline, .latex-math-display")) {
                    out.push(n);
                }
            }
        }
        return out;
    }

    function hydrate(root) {
        if (!root) return Promise.resolve();

        // Older format written by the previous offline editor.
        root.querySelectorAll(".offline-math-node[data-formula]").forEach(function (old) {
            old.replaceWith(createNode(old.getAttribute("data-formula"), false));
        });

        textNodesWithMath(root).forEach(function (textNode) {
            var text = textNode.nodeValue;
            var re = /\\\(([\s\S]+?)\\\)|\\\[([\s\S]+?)\\\]/g;
            var last = 0, m, frag = document.createDocumentFragment(), found = false;
            while ((m = re.exec(text))) {
                found = true;
                if (m.index > last) frag.appendChild(document.createTextNode(text.slice(last, m.index)));
                var isDisplay = m[2] !== undefined;
                frag.appendChild(createNode((isDisplay ? m[2] : m[1]).trim(), isDisplay));
                last = re.lastIndex;
            }
            if (!found) return;
            if (last < text.length) frag.appendChild(document.createTextNode(text.slice(last)));
            textNode.parentNode.replaceChild(frag, textNode);
        });

        var nodes = Array.prototype.slice.call(
            root.querySelectorAll(".latex-math-inline, .latex-math-display"));
        return nodes.length ? typeset(nodes) : Promise.resolve();
    }

    // Clean HTML for saving: equations back to \( ... \), no MathJax markup.
    function serialize(editor) {
        var clone = editor.cloneNode(true);
        clone.querySelectorAll(".latex-math-inline, .latex-math-display, .offline-math-node").forEach(function (n) {
            var latex = n.getAttribute("data-latex") || n.getAttribute("data-formula") || "";
            var display = n.getAttribute("data-latex-mode") === "display" || n.classList.contains("latex-math-display");
            n.replaceWith(document.createTextNode(display ? "\\[" + latex + "\\]" : "\\(" + latex + "\\)"));
        });
        clone.querySelectorAll("mjx-container, mjx-assistive-mml").forEach(function (n) { n.remove(); });
        clone.querySelectorAll(".resize-handle").forEach(function (n) { n.remove(); });
        return clone.innerHTML.replace(/\u200B/g, "").trim();
    }

    /* ---------- popup ---------- */
    var state = { editor: null, range: null, timer: null };

    function ui() {
        return {
            modal: document.getElementById("questionMathModal"),
            input: document.getElementById("questionMathInput"),
            preview: document.getElementById("questionMathLivePreview"),
            error: document.getElementById("questionMathError"),
            display: document.getElementById("questionMathDisplayMode"),
            cancel: document.getElementById("questionCancelMath"),
            insert: document.getElementById("questionInsertMath")
        };
    }

    function rememberSelection(editor) {
        var sel = window.getSelection();
        state.range = null;
        if (sel && sel.rangeCount > 0) {
            var r = sel.getRangeAt(0);
            if (editor.contains(r.commonAncestorContainer)) state.range = r.cloneRange();
        }
    }

    function renderPreview() {
        var el = ui();
        if (el.error) el.error.textContent = "";
        var latex = el.input.value.trim();
        if (!latex) { el.preview.textContent = "Enter a formula to see its preview."; return; }
        var isDisplay = !!(el.display && el.display.checked);
        el.preview.replaceChildren();
        var node = createNode(latex, isDisplay);
        el.preview.appendChild(node);
        typeset([node]).catch(function () {
            if (el.error) el.error.textContent = "Unable to render this formula. Check the LaTeX syntax.";
        });
    }

    function openModal(editor) {
        var el = ui();
        if (!el.modal || !el.input) { alert("Math popup is not available on this page."); return; }
        state.editor = editor;
        rememberSelection(editor);
        el.input.value = "";
        if (el.display) el.display.checked = false;
        if (el.error) el.error.textContent = "";
        el.preview.textContent = "Enter a formula to see its preview.";
        el.modal.style.display = "flex";
        el.modal.classList.add("is-open");
        el.input.focus();
    }

    function closeModal() {
        var el = ui();
        if (el.modal) { el.modal.style.display = "none"; el.modal.classList.remove("is-open"); }
        if (state.editor) state.editor.focus();
        state.range = null;
    }

    function insertFormula() {
        var el = ui();
        var latex = el.input.value.trim();
        if (!latex) { if (el.error) el.error.textContent = "Please enter a LaTeX formula."; return; }
        var editor = state.editor;
        if (!editor) { if (el.error) el.error.textContent = "Click inside the question or an option first."; return; }

        var isDisplay = !!(el.display && el.display.checked);
        var node = createNode(latex, isDisplay);

        editor.focus();
        var sel = window.getSelection();
        var range = state.range;
        if (!range || !editor.contains(range.commonAncestorContainer)) {
            range = document.createRange();
            range.selectNodeContents(editor);
            range.collapse(false);
        }
        sel.removeAllRanges();
        sel.addRange(range);
        range.deleteContents();
        range.insertNode(node);

        var spacer = document.createTextNode("\u00A0");
        node.parentNode.insertBefore(spacer, node.nextSibling);
        range.setStartAfter(spacer);
        range.collapse(true);
        sel.removeAllRanges();
        sel.addRange(range);

        typeset([node]);
        closeModal();
        editor.dispatchEvent(new Event("input", { bubbles: true }));
    }

    function bind() {
        var el = ui();
        if (el.modal && el.input) {
            el.input.addEventListener("input", function () {
                window.clearTimeout(state.timer);
                state.timer = window.setTimeout(renderPreview, 150);
            });
            if (el.display) el.display.addEventListener("change", renderPreview);
            if (el.cancel) el.cancel.addEventListener("click", function (e) { e.preventDefault(); closeModal(); });
            if (el.insert) el.insert.addEventListener("click", function (e) { e.preventDefault(); insertFormula(); });
            el.modal.addEventListener("click", function (e) { if (e.target === el.modal) closeModal(); });
            document.addEventListener("keydown", function (e) {
                if (e.key === "Escape" && el.modal.style.display === "flex") closeModal();
            });
            document.querySelectorAll(".latex-example").forEach(function (b) {
                b.addEventListener("click", function () {
                    el.input.value = b.getAttribute("data-latex") || "";
                    renderPreview();
                    el.input.focus();
                });
            });
        }

        document.querySelectorAll("#questionMathBtn, .editor-math-btn, .vetter-math-btn, .question-math-btn")
            .forEach(function (button) {
                // mousedown keeps the caret inside the editor while the popup opens
                button.addEventListener("mousedown", function (e) { e.preventDefault(); });
                button.addEventListener("click", function (e) {
                    e.preventDefault();
                    var container = button.closest(".question-editor-container");
                    var editor = container ? container.querySelector(".question-editor") : null;
                    if (!editor) editor = document.getElementById("questionEditor");
                    if (editor) openModal(editor);
                });
            });

        document.querySelectorAll(".question-editor, .qbms-math-content").forEach(hydrate);
    }

    window.QBMSMath = { hydrate: hydrate, serialize: serialize, typeset: typeset };

    function start() {
        bind();
        // MathJax may finish loading after DOMContentLoaded: render again when it is ready.
        var tries = 0;
        var timer = window.setInterval(function () {
            tries++;
            if (mj()) {
                window.clearInterval(timer);
                document.querySelectorAll(".question-editor, .qbms-math-content").forEach(function (root) {
                    root.querySelectorAll("[data-fallback]").forEach(function (n) { n.removeAttribute("data-fallback"); });
                    hydrate(root);
                });
            } else if (tries > 40) {
                window.clearInterval(timer);
            }
        }, 250);
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", start);
    else start();
})();
