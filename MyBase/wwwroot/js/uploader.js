(function () {
    function humanSize(bytes) {
        const units = ["B", "KB", "MB", "GB", "TB"];
        let i = 0, n = bytes;
        while (n >= 1024 && i < units.length - 1) { n /= 1024; i++; }
        return `${n.toFixed(2)} ${units[i]}`;
    }

    async function uploadSequential(form, files, opts) {
        const modal = document.querySelector(opts.modalSelector);
        const list = modal.querySelector("#uploadList");
        const overallProg = modal.querySelector("#uploadOverallProgress");
        const overallTxt = modal.querySelector("#uploadOverallStatus");

        modal.style.display = "flex";
        list.innerHTML = "";

        const items = [];
        let totalBytes = 0;
        for (const f of files) {
            totalBytes += f.size;
            const li = document.createElement("li");
            li.className = "upload-item";
            li.innerHTML = `
        <div class="upload-row">
          <div class="upload-name">${f.name}</div>
          <div class="upload-size">${humanSize(f.size)}</div>
        </div>
        <progress value="0" max="100"></progress>
        <div class="upload-status">0%</div>
      `;
            list.appendChild(li);
            items.push({ file: f, li, progress: li.querySelector("progress"), status: li.querySelector(".upload-status") });
        }

        let uploadedBytes = 0;

        for (const item of items) {
            await new Promise((resolve, reject) => {
                const fd = new FormData(form);
                fd.append(opts.inputName, item.file);

                const xhr = new XMLHttpRequest();
                xhr.open("POST", opts.url, true);
                xhr.setRequestHeader("X-Requested-With", "XMLHttpRequest");

                xhr.upload.onprogress = (e) => {
                    if (!e.lengthComputable) return;
                    


                    const pct = Math.round((e.loaded / e.total) * 100);
                    item.progress.value = Math.min(pct, 100);
                    item.status.textContent = Math.min(pct, 100) + "%";


                    const currentTotal = uploadedBytes + e.loaded;
                    const overallPct = Math.round((currentTotal / totalBytes) * 100);
                    overallProg.value = Math.min(overallPct, 100);
                    overallTxt.textContent = Math.min(overallPct, 100) + "%";
                };

                xhr.onload = () => {
                    if (xhr.status >= 200 && xhr.status < 300) {
                        item.progress.value = 100;
                        item.status.textContent = "✅ Fertig";
                        uploadedBytes += item.file.size;
                        resolve();
                    } else {
                        item.status.textContent = "❌ Fehler (" + xhr.status + ")";
                        reject(new Error("Upload failed: " + xhr.status));
                    }
                };

                xhr.onerror = () => {
                    item.status.textContent = "❌ Netzwerkfehler";
                    reject(new Error("Network error"));
                };

                xhr.send(fd);
            }).catch(err => { console.error(err); });
        }

        overallProg.value = 100;
        overallTxt.textContent = "100%";

        if (opts.autoReload) {
            setTimeout(() => window.location.reload(), 800);
        } else {
            setTimeout(() => { modal.style.display = "none"; }, 800);
        }
    }

    function attach(form, options) {
        const opts = Object.assign({
            url: form.dataset.targetUrl || "?handler=Upload",
            inputName: form.dataset.inputName || "FilesToUpload",
            modalSelector: form.dataset.modal || "#uploadModal",
            autoReload: (form.dataset.autoReload || "true") === "true"
        }, options || {});

        form.addEventListener("submit", function (e) {
            e.preventDefault();
            const input = form.querySelector(`input[type="file"][name="${opts.inputName}"]`) || form.querySelector('input[type="file"]');
            if (!input || input.files.length === 0) return;
            uploadSequential(form, Array.from(input.files), opts);
        });
    }

    function autoAttach() {
        document.querySelectorAll("form[data-uploader]").forEach(f => attach(f, {}));
    }

    window.Uploader = { attach, autoAttach };
})();
