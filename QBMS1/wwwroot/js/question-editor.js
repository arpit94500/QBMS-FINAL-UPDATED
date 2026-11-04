/*
 * QBMS question editor (Setter: Create Question and Edit Question)
 * ----------------------------------------------------------------
 * Replaces the previous 1,490-line file, which contained two copies of the editor
 * (two submit handlers, two Math implementations) and, because the page loaded it
 * twice, ran everything twice.
 *
 * Equations are handled by qbms-math.js (QBMSMath); this file only
 *   - wires the rich-text toolbars (bold / italic / underline / image),
 *   - copies each editor into its hidden MVC field on submit,
 *   - keeps the hierarchy fields in step with the Topic drop-down,
 *   - disables difficulty levels whose quota is already complete,
 *   - runs the duplicate check (button + automatic) against the Python API,
 *   - makes Save / Next post the right NavigationAction.
 * All element ids are unchanged.
 */
(function () {
    "use strict";

    function byId(id) { return document.getElementById(id); }

    document.addEventListener("DOMContentLoaded", function () {
        var form = byId("questionForm");
        if (!form) { return; }

        var pairs = [
            ["questionEditor", "hiddenQuestionText", "the question"],
            ["option1Editor", "hiddenOption1Text", "Option 1"],
            ["option2Editor", "hiddenOption2Text", "Option 2"],
            ["option3Editor", "hiddenOption3Text", "Option 3"],
            ["option4Editor", "hiddenOption4Text", "Option 4"]
        ];

        var activeEditor = byId("questionEditor");
        var savedRange = null;

        /* ---------------------------------------------------------------
           Editors: remember the caret, toolbar, images
           --------------------------------------------------------------- */
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

        function insertAtCaret(node) {
            restore();
            var sel = window.getSelection();
            var range = sel.getRangeAt(0);
            range.deleteContents();
            range.insertNode(node);
            range.setStartAfter(node);
            range.collapse(true);
            sel.removeAllRanges();
            sel.addRange(range);
            savedRange = range.cloneRange();
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
                    editor.focus();
                });
            });

            var imageInput = container.querySelector(".editor-image-input, #questionImageInput");
            var imageButton = container.querySelector(".editor-image-btn, #questionImageBtn");
            if (imageInput && imageButton) {
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
                        var wrapper = document.createElement("span");
                        wrapper.className = "resizable-img-wrapper";
                        wrapper.setAttribute("contenteditable", "false");
                        var img = document.createElement("img");
                        img.src = ev.target.result;
                        img.style.width = "200px";
                        img.style.maxWidth = "100%";
                        img.style.height = "auto";
                        var handle = document.createElement("span");
                        handle.className = "resize-handle";
                        wrapper.appendChild(img);
                        wrapper.appendChild(handle);
                        insertAtCaret(wrapper);
                        insertAtCaret(document.createTextNode("\u200B"));
                        imageInput.value = "";
                        editor.focus();
                    };
                    reader.onerror = function () { alert("Unable to read image."); imageInput.value = ""; };
                    reader.readAsDataURL(file);
                });
            }

            // Drag the handle to resize a picture.
            editor.addEventListener("mousedown", function (e) {
                var handle = e.target.closest ? e.target.closest(".resize-handle") : null;
                if (!handle) { return; }
                e.preventDefault();
                var img = handle.parentNode.querySelector("img");
                if (!img) { return; }
                var startX = e.clientX, startWidth = img.offsetWidth;
                function move(ev) {
                    img.style.width = Math.max(50, Math.min(1000, startWidth + ev.clientX - startX)) + "px";
                }
                function stop() {
                    document.removeEventListener("mousemove", move);
                    document.removeEventListener("mouseup", stop);
                }
                document.addEventListener("mousemove", move);
                document.addEventListener("mouseup", stop);
            });
        });

        /* ---------------------------------------------------------------
           Topic -> hierarchy fields
           --------------------------------------------------------------- */
        var topicSelect = byId("topicSelect");
        function setValue(id, value) { var el = byId(id); if (el) { el.value = value || ""; } }

        function updateHierarchy() {
            if (!topicSelect) { return; }
            var o = topicSelect.options[topicSelect.selectedIndex];
            if (!o || !o.value) { setValue("Question_TopicId", ""); setValue("topicIdDisplay", ""); return; }
            setValue("Question_MainExamId", o.getAttribute("data-main-exam-id"));
            setValue("Question_PaperId", o.getAttribute("data-paper-id"));
            setValue("Question_SectionId", o.getAttribute("data-section-id"));
            setValue("Question_SubsectionId", o.getAttribute("data-subsection-id"));
            setValue("Question_TopicId", o.value);
            setValue("examNameDisplay", o.getAttribute("data-exam-name"));
            setValue("paperNameDisplay", o.getAttribute("data-paper-name"));
            setValue("sectionNameDisplay", o.getAttribute("data-section-name"));
            setValue("subsectionNameDisplay", o.getAttribute("data-subsection-name"));
            setValue("topicIdDisplay", o.value);

            // A new question takes the question type of the chosen TOS topic.
            var typeSelect = form.querySelector("[name='Question.QuestionType']");
            var qid = byId("Question_QuestionId");
            var topicType = o.getAttribute("data-topic-type");
            if (typeSelect && topicType && (!qid || !qid.value || qid.value === "0")) {
                typeSelect.value = topicType;
            }
        }
        if (topicSelect) {
            topicSelect.addEventListener("change", updateHierarchy);
            updateHierarchy();
        }

        /* ---------------------------------------------------------------
           Quota: disable difficulty levels that are already complete
           --------------------------------------------------------------- */
        var difficulty = byId("difficultySelect");
        function applyQuota() {
            // On Create Question the difficulty is pre-filled from the TOS quota and posted as a hidden field,
            // so there is no drop-down to adjust there (Edit Question still has one).
            if (!difficulty || !difficulty.options) { return; }
            var map = {
                EASY: difficulty.getAttribute("data-easy-remaining"),
                MEDIUM: difficulty.getAttribute("data-medium-remaining"),
                HARD: difficulty.getAttribute("data-hard-remaining")
            };
            if (map.EASY === null) { return; }          // no quota information on this page
            var current = (difficulty.getAttribute("data-current-difficulty") || "").toUpperCase();
            var saved = difficulty.getAttribute("data-saved") === "true";   // an existing DRAFT keeps its own slot

            Array.prototype.forEach.call(difficulty.options, function (opt) {
                var key = (opt.value || "").toUpperCase();
                if (!map.hasOwnProperty(key)) { return; }
                var remaining = parseInt(map[key], 10) || 0;
                if (saved && key === current) { remaining += 1; }
                var base = opt.getAttribute("data-label") || opt.text.replace(/ \(quota (full|complete)\)$/, "");
                opt.setAttribute("data-label", base);
                opt.disabled = remaining <= 0;
                opt.text = remaining <= 0 ? base + " (quota complete)" : base + " (" + remaining + " left)";
            });

            var sel = difficulty.options[difficulty.selectedIndex];
            if (sel && sel.disabled) {
                var firstFree = Array.prototype.filter.call(difficulty.options, function (o) { return o.value && !o.disabled; })[0];
                difficulty.value = firstFree ? firstFree.value : "";
            }
        }
        applyQuota();

        /* ---------------------------------------------------------------
           Duplicate check (Python similarity API through the Setter controller)
           --------------------------------------------------------------- */
        var dupUrl = form.getAttribute("data-duplicate-url");
        var dupPanel = byId("duplicatePanel");
        var dupButton = byId("checkDuplicateBtn");
        var lastChecked = null;
        var dupBusy = false;

        function plainText(editor) {
            var clone = editor.cloneNode(true);
            clone.querySelectorAll("img").forEach(function (i) { i.remove(); });
            return (window.QBMSMath ? window.QBMSMath.serialize(clone) : clone.innerHTML).replace(/<[^>]+>/g, " ")
                .replace(/&nbsp;/g, " ").replace(/\s+/g, " ").trim();
        }

        function showDuplicate(result) {
            if (!dupPanel) { return; }
            dupPanel.className = "duplicate-panel";
            dupPanel.textContent = "";
            function line(text, cls) {
                var p = document.createElement("div");
                if (cls) { p.className = cls; }
                p.textContent = text;
                dupPanel.appendChild(p);
                return p;
            }
            if (!result || result.available === false) {
                dupPanel.classList.add("is-warning");
                line(result && result.message ? result.message : "Duplicate check is not available right now. It will be tried again when you save.");
                return;
            }
            if (!result.matches || result.matches.length === 0) {
                dupPanel.classList.add("is-clear");
                line("No similar question found. This question can be saved.");
                return;
            }
            dupPanel.classList.add(result.isDuplicate ? "is-duplicate" : "is-warning");
            line(result.isDuplicate
                ? "Possible duplicate: this question is at or above the " + result.threshold + "% similarity limit and cannot be saved."
                : "Closest existing questions (below the " + result.threshold + "% limit):", "duplicate-title");
            result.matches.forEach(function (m) {
                line("ID " + m.questionId + "  |  " + Number(m.similarity).toFixed(2) + "%  |  " + (m.text || ""), "duplicate-match");
            });
        }

        function runDuplicateCheck(force) {
            var q = byId("questionEditor");
            if (!dupUrl || !q || dupBusy) { return; }
            var text = plainText(q);
            var hasPicture = !!q.querySelector("img");
            if (!text && !hasPicture) { return; }
            var html = window.QBMSMath ? window.QBMSMath.serialize(q) : q.innerHTML;
            if (!force && lastChecked === html) { return; }
            lastChecked = html;
            dupBusy = true;
            if (dupButton) { dupButton.disabled = true; dupButton.textContent = "Checking..."; }
            if (dupPanel) { dupPanel.className = "duplicate-panel is-checking"; dupPanel.textContent = "Checking for similar questions..."; }

            var tokenInput = form.querySelector("input[name='__RequestVerificationToken']");
            var idInput = byId("Question_QuestionId");
            fetch(dupUrl, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": tokenInput ? tokenInput.value : ""
                },
                body: JSON.stringify({ questionText: html, questionId: idInput && idInput.value ? parseInt(idInput.value, 10) : 0 })
            }).then(function (r) { return r.json(); })
                .then(showDuplicate)
                .catch(function () { showDuplicate({ available: false }); })
                .then(function () {
                    dupBusy = false;
                    if (dupButton) { dupButton.disabled = false; dupButton.textContent = "Check duplicates"; }
                });
        }

        if (dupButton) { dupButton.addEventListener("click", function () { runDuplicateCheck(true); }); }
        var qEditor = byId("questionEditor");
        if (qEditor && dupUrl) {
            qEditor.addEventListener("blur", function () { window.setTimeout(function () { runDuplicateCheck(false); }, 150); });
        }

        /* ---------------------------------------------------------------
           Save / Next buttons choose the navigation action
           --------------------------------------------------------------- */
        var nav = byId("NavigationAction");
        [["saveQuestionBtn", "save"], ["nextQuestionBtn", "next"]].forEach(function (pair) {
            var button = byId(pair[0]);
            if (button && nav) {
                button.addEventListener("click", function () { nav.value = pair[1]; });
            }
        });

        /* ---------------------------------------------------------------
           Submit: copy editors into hidden fields and validate
           --------------------------------------------------------------- */
        function hasContent(html) {
            if (!html) { return false; }
            if (/<img\b/i.test(html)) { return true; }
            var text = html.replace(/<br\s*\/?>/gi, "").replace(/&nbsp;/gi, "").replace(/<[^>]*>/g, "").replace(/\u200B/g, "").trim();
            return text.length > 0;
        }

        function fail(message, editor) {
            alert(message);
            if (editor) { editor.focus(); }
        }

        form.addEventListener("submit", function (event) {
            var typeSelect = form.querySelector("[name='Question.QuestionType'], [name='QuestionType']");
            var isObjective = !typeSelect || (typeSelect.value || "").toUpperCase() !== "SUBJECTIVE";

            pairs.forEach(function (pair) {
                var editor = byId(pair[0]), hidden = byId(pair[1]);
                if (editor && hidden) {
                    hidden.value = window.QBMSMath ? window.QBMSMath.serialize(editor) : editor.innerHTML.trim();
                }
            });
            if (topicSelect) { setValue("Question_TopicId", topicSelect.value); }

            if (topicSelect && (!topicSelect.value || topicSelect.value === "0")) {
                event.preventDefault();
                return fail("Please select a topic.", topicSelect);
            }
            // pre-filled topic (hidden field) must be present
            var prefilledTopic = byId("Question_TopicId");
            if (!topicSelect && prefilledTopic && (!prefilledTopic.value || prefilledTopic.value === "0")) {
                event.preventDefault();
                return fail("No topic has quota left for this assignment, so a new question cannot be created.");
            }
            for (var i = 0; i < pairs.length; i++) {
                var editor = byId(pairs[i][0]), hidden = byId(pairs[i][1]);
                if (!editor || !hidden) { continue; }
                if (i > 0 && !isObjective) { continue; }               // options are only needed for MCQ
                if (!hasContent(hidden.value)) {
                    event.preventDefault();
                    return fail("Please enter " + pairs[i][2] + ".", editor);
                }
            }
            if (difficulty && !difficulty.value) {
                event.preventDefault();
                return fail("No difficulty level has quota left, so a new question cannot be saved.", difficulty);
            }

            // correct answer: qbms-correct-answer.js has already copied the chosen option
            var correct = byId("hiddenCorrectAnswerText");
            if (isObjective && correct && !hasContent(correct.value)) {
                event.preventDefault();
                return fail("Please select the correct answer.", byId("correctAnswerSelect"));
            }

            // disable after the browser has taken the form data
            var submitter = event.submitter;
            if (submitter && submitter.id) {
                window.setTimeout(function () { submitter.disabled = true; submitter.textContent = "Saving..."; }, 0);
            }
        });
    });
})();
