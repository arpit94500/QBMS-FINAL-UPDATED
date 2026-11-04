/*
 * QBMS image compression
 * ----------------------
 * Every image that goes into a question / option editor is limited to
 * MAX_BYTES (500 KB). Images are compressed in the browser BEFORE they are
 * inserted into the editor, so the compressed image is what gets posted and
 * stored in the database.
 *
 * Works for all editors (Setter create / edit, Vetter review) without
 * changing their own scripts:
 *   1. file picker  - intercepts the "change" event of .editor-image-input
 *                     and .vetter-image-input, swaps in the compressed file.
 *   2. paste / drop - images that arrive without the file picker are
 *                     compressed right after they land in the editor.
 *   3. submit       - last safety net: any image still larger than the
 *                     limit (e.g. an old saved question) is compressed
 *                     before the form is sent.
 *
 * Images already at or below the limit are left untouched.
 */
(function () {
    "use strict";

    var MAX_BYTES = 500 * 1024;      // fixed limit: 500 KB
    var MAX_DIMENSION = 1600;        // longest side to start from (px)
    var MIN_DIMENSION = 240;         // never shrink below this (px)
    var QUALITIES = [0.85, 0.75, 0.65, 0.55, 0.45, 0.35];
    var SHRINK_STEP = 0.8;

    /* ---------------------------------------------------- helpers */

    function kb(bytes) {
        return Math.round(bytes / 1024) + " KB";
    }

    function dataUrlBytes(src) {
        var comma = src.indexOf(",");
        if (comma < 0) { return 0; }
        var length = src.length - comma - 1;
        var padding = 0;
        if (src.charAt(src.length - 1) === "=") { padding++; }
        if (src.charAt(src.length - 2) === "=") { padding++; }
        return Math.floor(length * 3 / 4) - padding;
    }

    function loadImage(url) {
        return new Promise(function (resolve, reject) {
            var img = new Image();
            img.onload = function () { resolve(img); };
            img.onerror = function () { reject(new Error("Unable to read image.")); };
            img.src = url;
        });
    }

    function canvasToBlob(canvas, type, quality) {
        return new Promise(function (resolve) {
            canvas.toBlob(function (blob) { resolve(blob); }, type, quality);
        });
    }

    function blobToDataUrl(blob) {
        return new Promise(function (resolve, reject) {
            var reader = new FileReader();
            reader.onload = function () { resolve(reader.result); };
            reader.onerror = function () { reject(new Error("Unable to read image.")); };
            reader.readAsDataURL(blob);
        });
    }

    function encode(img, width, height, quality) {
        var canvas = document.createElement("canvas");
        canvas.width = width;
        canvas.height = height;

        var ctx = canvas.getContext("2d");

        // JPEG has no transparency: paint white behind PNG/GIF/WebP.
        ctx.fillStyle = "#ffffff";
        ctx.fillRect(0, 0, width, height);
        ctx.drawImage(img, 0, 0, width, height);

        return canvasToBlob(canvas, "image/jpeg", quality);
    }

    /* ------------------------------------------- core compression */

    // src: an object URL or a data URL. Resolves to a JPEG Blob that is
    // <= MAX_BYTES, or the smallest one it could make (null on failure).
    async function compressSource(src) {
        var img = await loadImage(src);

        var width = img.naturalWidth || 800;
        var height = img.naturalHeight || 600;

        var scale = Math.min(1, MAX_DIMENSION / Math.max(width, height));
        var best = null;

        for (var round = 0; round < 15; round++) {

            var w = Math.max(1, Math.round(width * scale));
            var h = Math.max(1, Math.round(height * scale));

            for (var i = 0; i < QUALITIES.length; i++) {
                var out = await encode(img, w, h, QUALITIES[i]);

                if (!out) { continue; }

                if (!best || out.size < best.size) { best = out; }

                if (out.size <= MAX_BYTES) { return out; }
            }

            if (Math.max(w, h) <= MIN_DIMENSION) { break; }

            scale *= SHRINK_STEP;
        }

        return best;
    }

    async function compressBlob(blob) {
        var url = URL.createObjectURL(blob);
        try {
            return await compressSource(url);
        } finally {
            URL.revokeObjectURL(url);
        }
    }

    /* ------------------------------------------------- small notice */

    var noticeTimer = null;

    function notice(message, isError) {
        var box = document.getElementById("qbmsImageNotice");

        if (!box) {
            box = document.createElement("div");
            box.id = "qbmsImageNotice";
            box.style.cssText =
                "position:fixed;right:16px;bottom:16px;z-index:99999;" +
                "max-width:340px;padding:10px 14px;border-radius:6px;" +
                "font:14px/1.4 Arial,sans-serif;color:#fff;" +
                "box-shadow:0 2px 8px rgba(0,0,0,.3);";
            document.body.appendChild(box);
        }

        box.style.background = isError ? "#b02a37" : "#2b6cb0";
        box.textContent = message;
        box.style.display = "block";

        clearTimeout(noticeTimer);
        noticeTimer = setTimeout(function () {
            box.style.display = "none";
        }, isError ? 7000 : 4000);
    }

    /* ----------------------------------------- 1. file picker input */

    function isEditorImageInput(el) {
        return el && el.tagName === "INPUT" && el.type === "file" &&
            el.classList &&
            (el.classList.contains("editor-image-input") ||
                el.classList.contains("vetter-image-input"));
    }

    document.addEventListener("change", function (e) {
        var input = e.target;

        if (!isEditorImageInput(input) || input.__qbmsPassThrough) {
            return;
        }

        var file = input.files && input.files[0];

        // Not an image -> the editor's own script shows its message.
        if (!file || !file.type || file.type.indexOf("image/") !== 0) {
            return;
        }

        // Already within the limit -> use it as it is.
        if (file.size <= MAX_BYTES) {
            return;
        }

        // Hold back the editor's own handlers until compression is done.
        e.stopImmediatePropagation();

        notice("Compressing image (" + kb(file.size) + ")...");

        compressBlob(file).then(function (blob) {

            if (!blob || blob.size > MAX_BYTES) {
                input.value = "";
                notice("This image could not be reduced to " + kb(MAX_BYTES) +
                    ". Please choose a smaller image.", true);
                return;
            }

            var name = (file.name || "image").replace(/\.[^.]+$/, "") + ".jpg";
            var compressed = new File([blob], name, {
                type: "image/jpeg",
                lastModified: Date.now()
            });

            var transfer = new DataTransfer();
            transfer.items.add(compressed);
            input.files = transfer.files;

            // Let the editor's own handlers run, now with the small file.
            input.__qbmsPassThrough = true;
            try {
                input.dispatchEvent(new Event("change", { bubbles: true }));
            } finally {
                input.__qbmsPassThrough = false;
            }

            notice("Image compressed: " + kb(file.size) + " \u2192 " + kb(blob.size));

        }).catch(function () {
            input.value = "";
            notice("Unable to compress this image. Please try another one.", true);
        });

    }, true);

    /* -------------------------------- 2. paste / drop and 3. submit */

    // Re-compress base64 images inside an editor that are over the limit.
    // Resolves to the number of images that were replaced.
    async function compressEmbedded(root) {
        var images = root.querySelectorAll("img[src^='data:image/']");
        var replaced = 0;

        for (var i = 0; i < images.length; i++) {
            var img = images[i];
            var src = img.getAttribute("src") || "";

            if (/^data:image\/svg/i.test(src)) { continue; }

            if (dataUrlBytes(src) <= MAX_BYTES) { continue; }

            try {
                var blob = await compressSource(src);

                if (!blob || blob.size > MAX_BYTES) {
                    notice("An image could not be reduced to " + kb(MAX_BYTES) +
                        ". Please replace it with a smaller one.", true);
                    continue;
                }

                img.setAttribute("src", await blobToDataUrl(blob));
                replaced++;
            } catch (ex) {
                notice("Unable to compress an image in the editor.", true);
            }
        }

        return replaced;
    }

    function editorOf(target) {
        return target && target.closest ? target.closest(".question-editor") : null;
    }

    function afterPasteOrDrop(e) {
        var editor = editorOf(e.target);
        if (!editor) { return; }

        setTimeout(function () {
            compressEmbedded(editor).then(function (n) {
                if (n > 0) { notice("Pasted image compressed to under " + kb(MAX_BYTES) + "."); }
            });
        }, 100);
    }

    document.addEventListener("paste", afterPasteOrDrop, true);
    document.addEventListener("drop", afterPasteOrDrop, true);

    function hasOversizeImage(form) {
        var images = form.querySelectorAll(".question-editor img[src^='data:image/']");

        for (var i = 0; i < images.length; i++) {
            var src = images[i].getAttribute("src") || "";
            if (!/^data:image\/svg/i.test(src) && dataUrlBytes(src) > MAX_BYTES) {
                return true;
            }
        }
        return false;
    }

    var resubmitting = false;

    document.addEventListener("submit", function (e) {
        var form = e.target;

        if (resubmitting || !form || !form.querySelectorAll) { return; }

        if (!hasOversizeImage(form)) { return; }

        // Stop this submit, compress, then submit again.
        e.preventDefault();
        e.stopImmediatePropagation();

        var submitter = e.submitter || null;

        notice("Compressing images before saving...");

        var editors = Array.prototype.slice.call(form.querySelectorAll(".question-editor"));

        Promise.all(editors.map(compressEmbedded)).then(function () {
            resubmitting = true;
            try {
                if (typeof form.requestSubmit === "function") {
                    try {
                        form.requestSubmit(submitter || undefined);
                    } catch (ex) {
                        form.requestSubmit();
                    }
                } else {
                    // Older browsers: run the submit handlers, then send.
                    var again = new Event("submit", { bubbles: true, cancelable: true });
                    if (form.dispatchEvent(again)) {
                        if (submitter && submitter.name) {
                            var hidden = document.createElement("input");
                            hidden.type = "hidden";
                            hidden.name = submitter.name;
                            hidden.value = submitter.value;
                            form.appendChild(hidden);
                        }
                        form.submit();
                    }
                }
            } finally {
                resubmitting = false;
            }
        });

    }, true);

    window.QBMSImage = {
        MAX_BYTES: MAX_BYTES,
        compressBlob: compressBlob,
        compressEmbedded: compressEmbedded,
        dataUrlBytes: dataUrlBytes
    };

})();
