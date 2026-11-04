/*
 * QBMS UI helpers - one shared script (loaded by _Layout).
 *   [data-table-filter="#tableId"]   live text filter for a table (input element)
 *   [data-export-table="#tableId"]   button: download the visible rows as CSV
 *   [data-papers-url][data-paper-target]  exam drop-down that refills a paper drop-down
 *   [data-report-tab="paneId"]       tab buttons that show / hide .report-pane blocks
 */
(function () {
    "use strict";

    function ready(fn) {
        if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", fn); else fn();
    }

    ready(function () {
        // live filter
        document.querySelectorAll("[data-table-filter]").forEach(function (input) {
            var table = document.querySelector(input.getAttribute("data-table-filter"));
            if (!table) return;
            var counterSelector = input.getAttribute("data-filter-count");
            var counter = counterSelector ? document.querySelector(counterSelector) : null;
            function apply() {
                var q = input.value.trim().toLowerCase(), shown = 0;
                table.querySelectorAll("tbody tr").forEach(function (tr) {
                    if (tr.hasAttribute("data-empty-row")) return;
                    var match = !q || tr.textContent.toLowerCase().indexOf(q) > -1;
                    if (match) { tr.removeAttribute("data-filtered"); shown++; } else { tr.setAttribute("data-filtered", "1"); }
                    if (!table.__qbmsPager) { tr.style.display = match ? "" : "none"; }
                });
                if (counter) counter.textContent = shown;
                if (table.__qbmsPager) { table.__qbmsPager.goTo(1); }
            }
            input.addEventListener("input", apply);
            apply();
        });

        // CSV export
        document.querySelectorAll("[data-export-table]").forEach(function (button) {
            button.addEventListener("click", function () {
                var table = document.querySelector(button.getAttribute("data-export-table"));
                if (!table) return;
                var lines = [];
                table.querySelectorAll("tr").forEach(function (tr) {
                    if (tr.hasAttribute("data-filtered")) return;   // all pages are exported, filtered-out rows are not
                    var cells = [];
                    tr.querySelectorAll("th,td").forEach(function (td) {
                        var text = (td.getAttribute("data-export") || td.textContent).replace(/\s+/g, " ").trim();
                        cells.push('"' + text.replace(/"/g, '""') + '"');
                    });
                    if (cells.length) lines.push(cells.join(","));
                });
                var blob = new Blob(["\ufeff" + lines.join("\r\n")], { type: "text/csv;charset=utf-8" });
                var a = document.createElement("a");
                a.href = URL.createObjectURL(blob);
                a.download = (button.getAttribute("data-export-name") || "report") + ".csv";
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
            });
        });

        // pagination for every table (add data-no-paginate to opt out; the activity log pages on the server)
        var PAGE_SIZES = [10, 25, 50, 100];
        document.querySelectorAll("table").forEach(function (table) {
            if (table.hasAttribute("data-no-paginate") || !table.tBodies.length) return;
            var body = table.tBodies[0];

            function rows() {
                return Array.prototype.filter.call(body.rows, function (tr) {
                    return !tr.hasAttribute("data-empty-row") && !tr.hasAttribute("data-filtered");
                });
            }

            var bar = document.createElement("div");
            bar.className = "qbms-pager";
            var info = document.createElement("span"); info.className = "qbms-pager-info";
            var buttons = document.createElement("span"); buttons.className = "qbms-pager-buttons";
            var sizeLabel = document.createElement("label"); sizeLabel.className = "qbms-pager-size"; sizeLabel.textContent = "Rows per page ";
            var size = document.createElement("select");
            PAGE_SIZES.forEach(function (n) { var o = document.createElement("option"); o.value = n; o.textContent = n; size.appendChild(o); });
            size.value = 10;
            sizeLabel.appendChild(size);
            bar.appendChild(info); bar.appendChild(buttons); bar.appendChild(sizeLabel);

            var host = table.parentNode;
            host.parentNode.insertBefore(bar, host.nextSibling);

            var page = 1;

            function button(text, target, disabled) {
                var bt = document.createElement("button");
                bt.type = "button"; bt.className = "btn btn-sm btn-outline-secondary"; bt.textContent = text; bt.disabled = disabled;
                bt.addEventListener("click", function () { render(target); });
                return bt;
            }

            function render(p) {
                var list = rows(), per = parseInt(size.value, 10), pages = Math.max(1, Math.ceil(list.length / per));
                page = Math.min(Math.max(1, p), pages);
                Array.prototype.forEach.call(body.rows, function (tr) {
                    if (tr.hasAttribute("data-empty-row")) { tr.style.display = list.length ? "none" : ""; return; }
                    tr.style.display = "none";
                });
                list.forEach(function (tr, i) {
                    tr.style.display = (i >= (page - 1) * per && i < page * per) ? "" : "none";
                });
                var from = list.length ? (page - 1) * per + 1 : 0, to = Math.min(list.length, page * per);
                info.textContent = "Showing " + from + " - " + to + " of " + list.length;
                buttons.innerHTML = "";
                bar.style.display = list.length <= PAGE_SIZES[0] && per === PAGE_SIZES[0] ? "none" : "";
                buttons.appendChild(button("\u00ab First", 1, page === 1));
                buttons.appendChild(button("\u2039 Previous", page - 1, page === 1));
                var label = document.createElement("span"); label.className = "qbms-pager-page"; label.textContent = "Page " + page + " of " + pages;
                buttons.appendChild(label);
                buttons.appendChild(button("Next \u203a", page + 1, page === pages));
                buttons.appendChild(button("Last \u00bb", pages, page === pages));
            }

            size.addEventListener("change", function () { render(1); });
            table.__qbmsPager = { goTo: render };
            render(1);
        });

        // exam -> paper
        document.querySelectorAll("[data-papers-url]").forEach(function (exam) {
            var paper = document.querySelector(exam.getAttribute("data-paper-target"));
            if (!paper) return;
            exam.addEventListener("change", function () {
                paper.innerHTML = '<option value="">All papers</option>';
                if (!exam.value) { paper.disabled = true; return; }
                fetch(exam.getAttribute("data-papers-url") + "?examId=" + encodeURIComponent(exam.value), { credentials: "same-origin" })
                    .then(function (r) { return r.json(); })
                    .then(function (items) {
                        items.forEach(function (item) {
                            var o = document.createElement("option");
                            o.value = item.id !== undefined ? item.id : item.Id;
                            o.textContent = (item.name !== undefined ? item.name : item.Name) + " (ID " + o.value + ")";
                            paper.appendChild(o);
                        });
                        paper.disabled = false;
                    })
                    .catch(function () { paper.disabled = true; });
            });
        });

        // report tabs
        var tabs = document.querySelectorAll("[data-report-tab]");
        tabs.forEach(function (tab) {
            tab.addEventListener("click", function () {
                tabs.forEach(function (t) { t.classList.toggle("is-active", t === tab); });
                document.querySelectorAll(".report-pane").forEach(function (p) {
                    p.hidden = p.id !== tab.getAttribute("data-report-tab");
                });
            });
        });

        // auto-submit selects
        document.querySelectorAll("select[data-autosubmit]").forEach(function (s) {
            s.addEventListener("change", function () { if (s.form) s.form.submit(); });
        });
    });
})();
