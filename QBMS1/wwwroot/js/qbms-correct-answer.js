/*
 * QBMS correct-answer selector
 * ----------------------------
 * The correct answer is chosen from a drop-down (Option 1 - Option 4)
 * instead of being typed. On submit the content of the chosen option is
 * copied into the field that is posted, so the value stored in the database
 * (and pushed to the question bank) has the same format as before.
 *
 * Runs in the capture phase so it executes before the page's own submit
 * handlers (which read the value and validate it).
 */
(function () {
    "use strict";

    function byId(id) {
        return id ? document.getElementById(id.trim()) : null;
    }

    function hasContent(el) {
        if (!el) { return false; }
        return el.textContent.replace(/[\u200B\s]/g, "").length > 0 ||
            !!el.querySelector("img, .offline-math-node, .latex-math-inline, .latex-math-display");
    }

    // Copies the selected option's content into the posted field.
    // Returns true when a value was set.
    function applySelection(select) {
        var n = parseInt(select.value, 10);

        if (!n) { return false; }

        var ids = (select.getAttribute("data-option-editors") || "").split(",");
        var source = byId(ids[n - 1]);

        if (!hasContent(source)) { return false; }

        // QBMSMath.serialize keeps the LaTeX source (\\( ... \\)) instead of MathJax's generated markup.
        var html = window.QBMSMath ? window.QBMSMath.serialize(source) : source.innerHTML.trim();

        var syncEditor = byId(select.getAttribute("data-sync-editor"));
        var hidden = byId(select.getAttribute("data-sync-hidden"));

        if (syncEditor) { syncEditor.innerHTML = html; }
        if (hidden) { hidden.value = html; }

        return true;
    }

    document.addEventListener("change", function (e) {
        if (e.target && e.target.id === "correctAnswerSelect") {
            applySelection(e.target);
        }
    });

    document.addEventListener("submit", function (e) {
        var form = e.target;
        var select = form && form.querySelector
            ? form.querySelector("#correctAnswerSelect")
            : null;

        if (!select) { return; }

        if (select.value) {
            // If the chosen option is empty, the page's own validation
            // reports "Please enter Option N".
            applySelection(select);
            return;
        }

        // A Vetter may reject a question without choosing an answer; the
        // existing answer is kept as it is.
        var submitter = e.submitter;
        if (submitter && submitter.value === "REJECT") { return; }

        e.preventDefault();
        e.stopImmediatePropagation();
        alert("Please select the Correct Answer (Option 1 to Option 4).");
        select.focus();

    }, true);

})();
