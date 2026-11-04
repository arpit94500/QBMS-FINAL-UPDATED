/*
 * QBMS Vetter review editor
 * -------------------------
 * - rich-text toolbar (bold / italic / underline / picture) for the question and options,
 * - equations through the shared qbms-math.js module (the Math button now opens the popup and
 *   saved \( ... \) LaTeX is shown as a rendered equation instead of code),
 * - on submit every editor is copied into its hidden field with the LaTeX source preserved.
 * Element ids are unchanged. The old copy of the Setter editor that used to sit at the end of
 * this file (it never ran on this page) has been removed.
 */
(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        var form = document.getElementById("vetterReviewForm");
        if (!form) { return; }

        var pairs = [
            ["vetterQuestionEditor", "hiddenVetterQuestionText"],
            ["vetterOption1Editor", "hiddenVetterOption1"],
            ["vetterOption2Editor", "hiddenVetterOption2"],
            ["vetterOption3Editor", "hiddenVetterOption3"],
            ["vetterOption4Editor", "hiddenVetterOption4"],
            ["vetterCorrectAnswerEditor", "hiddenVetterCorrectAnswer"]
        ];

        var activeEditor = null;
        var savedRange = null;

        function remember(editor) {
            activeEditor = editor;
            var sel = window.getSelection();
            if (sel && sel.rangeCount > 0) {
                var r = sel.getRangeAt(0);
                if (editor.contains(r.commonAncestorContainer)) { savedRange = r.cloneRange(); }
            }
        }

        function restore() {
            if (!activeEditor) { return; }
            activeEditor.focus();
            var sel = window.getSelection();
            var range = savedRange && activeEditor.contains(savedRange.commonAncestorContainer)
                ? savedRange
                : (function () { var r = document.createRange(); r.selectNodeContents(activeEditor); r.collapse(false); return r; })();
            sel.removeAllRanges();
            sel.addRange(range);
        }

        document.querySelectorAll(".question-editor").forEach(function (editor) {
            ["focus", "click", "keyup", "mouseup", "touchend"].forEach(function (evt) {
                editor.addEventListener(evt, function () { remember(editor); });
            });
        });

        document.querySelectorAll(".question-editor-container").forEach(function (container) {
            var editor = container.querySelector(".question-editor");
            if (!editor) { return; }

            container.querySelectorAll("button[data-command]").forEach(function (button) {
                button.addEventListener("mousedown", function (e) { e.preventDefault(); remember(editor); });
                button.addEventListener("click", function (e) {
                    e.preventDefault();
                    activeEditor = editor;
                    restore();
                    document.execCommand(button.getAttribute("data-command"), false, null);
                });
            });

            var imageButton = container.querySelector(".vetter-image-btn");
            var imageInput = container.querySelector(".vetter-image-input");
            if (imageButton && imageInput) {
                imageButton.addEventListener("mousedown", function (e) {
                    e.preventDefault();
                    remember(editor);
                    imageInput.click();
                });
                imageInput.addEventListener("change", function () {
                    var file = imageInput.files && imageInput.files[0];
                    if (!file) { return; }
                    if (!file.type || file.type.indexOf("image/") !== 0) {
                        alert("Please select a valid image.");
                        imageInput.value = "";
                        return;
                    }
                    var reader = new FileReader();
                    reader.onload = function (ev) {
                        activeEditor = editor;
                        restore();
                        var img = document.createElement("img");
                        img.src = ev.target.result;
                        img.style.maxWidth = "100%";
                        img.style.width = "200px";
                        img.style.height = "auto";
                        document.execCommand("insertHTML", false, img.outerHTML);
                    };
                    reader.readAsDataURL(file);
                    imageInput.value = "";
                });
            }
        });

        form.addEventListener("submit", function () {
            pairs.forEach(function (pair) {
                var editor = document.getElementById(pair[0]);
                var hidden = document.getElementById(pair[1]);
                if (editor && hidden) {
                    hidden.value = window.QBMSMath ? window.QBMSMath.serialize(editor) : editor.innerHTML.trim();
                }
            });
        });
    });
})();
