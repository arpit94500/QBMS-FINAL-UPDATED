/*
 * Login page: encrypt the password in the browser before it is sent.
 *
 * The server gives the page an RSA public key and a one-time challenge (data attributes on #loginForm).
 * On submit the password is encrypted with RSA-OAEP / SHA-256 together with the challenge, and only the
 * encrypted text is posted (hidden field EncryptedPassword). The visible password box has no name, so the
 * plain password is never part of the request.
 *
 * WebCrypto (crypto.subtle) is only available on HTTPS pages (and localhost).
 */
(function () {
    "use strict";

    function b64ToBytes(b64) {
        var bin = window.atob(b64);
        var bytes = new Uint8Array(bin.length);
        for (var i = 0; i < bin.length; i++) { bytes[i] = bin.charCodeAt(i); }
        return bytes;
    }

    function bytesToB64(buffer) {
        var bytes = new Uint8Array(buffer);
        var bin = "";
        for (var i = 0; i < bytes.length; i++) { bin += String.fromCharCode(bytes[i]); }
        return window.btoa(bin);
    }

    document.addEventListener("DOMContentLoaded", function () {
        var form = document.getElementById("loginForm");
        var plain = document.getElementById("loginPassword");
        var encrypted = document.getElementById("EncryptedPassword");
        if (!form || !plain || !encrypted) { return; }

        var publicKey = form.getAttribute("data-public-key");
        var challenge = form.getAttribute("data-challenge");
        var cryptoApi = window.crypto && window.crypto.subtle;
        var keyPromise = null;

        function showError(message) {
            var box = form.querySelector(".error-message");
            if (!box) {
                box = document.createElement("div");
                box.className = "error-message";
                form.insertBefore(box, form.firstChild);
            }
            box.textContent = message;
        }

        if (cryptoApi && publicKey) {
            // import the key once, up front
            keyPromise = cryptoApi.importKey(
                "spki",
                b64ToBytes(publicKey),
                { name: "RSA-OAEP", hash: "SHA-256" },
                false,
                ["encrypt"]);
        }

        var busy = false;

        form.addEventListener("submit", function (event) {
            if (busy) { event.preventDefault(); return; }

            if (!keyPromise) {
                event.preventDefault();
                showError("Secure login needs an HTTPS connection. Open the site with https:// and try again.");
                return;
            }

            if (!plain.value) { return; }          // the browser's "required" check handles an empty box

            event.preventDefault();
            busy = true;

            var message = JSON.stringify({ p: plain.value, n: challenge });
            var data = new TextEncoder().encode(message);

            keyPromise.then(function (key) {
                return cryptoApi.encrypt({ name: "RSA-OAEP" }, key, data);
            }).then(function (cipher) {
                encrypted.value = bytesToB64(cipher);
                plain.value = "";                   // nothing readable is left in the form
                form.submit();                      // programmatic submit does not re-run this handler
            }).catch(function () {
                busy = false;
                showError("The password could not be encrypted in this browser. Please try another browser.");
            });
        });
    });
})();
