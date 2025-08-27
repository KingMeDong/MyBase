(function () {
    const gallery = document.getElementById('gallery');
    if (!gallery) return;

    let page = 1;
    const pageSize = parseInt(gallery.dataset.pagesize || "60", 10);
    let loading = false;
    let hasMore = true;
    const sort = gallery.dataset.sort || "taken_desc";

    const fmt = (iso) => {
        if (!iso) return "";
        const d = new Date(iso);
        if (isNaN(d)) return "";
        return d.toLocaleString(); // lokal anzeigen
    };

    function getToken() {
        const el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : null;
    }

    function cardHtml(item) {
        // item kann string oder Objekt sein
        const name = typeof item === 'string' ? item : item.fileName;
        const uploadIso = typeof item === 'object' ? item.uploadDateUtc : null;
        const takenIso = typeof item === 'object' ? item.takenDateUtc : null;

        const enc = encodeURIComponent(name);
        const thumbUrl = `/thumbs/${enc}?w=320`;
        const fullUrl = `/media/images/${enc}`;
        const token = getToken() || "";

        const uploadTxt = uploadIso ? fmt(uploadIso) : "";
        const takenTxt = takenIso ? fmt(takenIso) : "";

        return `
      <div class="gallery-card" data-fn="${name}"
           data-upload="${uploadIso || ""}"
           data-taken="${takenIso || ""}">
        <a href="${fullUrl}" target="_blank" rel="noopener">
          <img src="${thumbUrl}" alt="${name}" loading="lazy" />
        </a>
        <div class="gallery-meta">
          <span class="file-name">${name}</span>
          <div class="actions">
            <a class="icon-btn" title="Download" href="/download/${enc}?scope=images" download>⬇️</a>
            <form method="post" action="?handler=Delete" onsubmit="return confirm('Bild wirklich löschen?');">
              <input type="hidden" name="__RequestVerificationToken" value="${token}">
              <input type="hidden" name="fileName" value="${name}">
              <button type="submit" class="icon-btn danger" title="Löschen">🗑️</button>
            </form>
          </div>
        </div>
        <div class="gallery-dates" style="padding:6px 8px;color:#8b949e;font-size:.85rem;">
          ⬆️ Upload: ${uploadTxt || "—"}
          ${takenTxt ? ` • 📸 ${takenTxt}` : ""}
        </div>
      </div>`;
    }

    async function loadNext() {
        if (loading || !hasMore) return;
        loading = true;
        page += 1;

        try {
            const res = await fetch(`/api/media/images?page=${page}&pageSize=${pageSize}&sort=${encodeURIComponent(sort)}`);
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const data = await res.json();
            hasMore = data.hasMore;

            const frag = document.createDocumentFragment();
            for (const item of data.items) {
                const div = document.createElement('div');
                div.innerHTML = cardHtml(item).trim();
                frag.appendChild(div.firstElementChild);
            }
            gallery.appendChild(frag);
        } catch (e) {
            console.error(e);
            hasMore = false;
        } finally {
            loading = false;
        }
    }

    const sentinel = document.getElementById('gallery-sentinel');
    if (sentinel) {
        const io = new IntersectionObserver((entries) => {
            entries.forEach(e => { if (e.isIntersecting) loadNext(); });
        }, { rootMargin: '600px 0px' });
        io.observe(sentinel);
    }

    // Upload ohne Reload: jetzt mit Metadaten
    document.addEventListener("upload:done", (e) => {
        const files = (e.detail && Array.isArray(e.detail.files)) ? e.detail.files : [];
        // files sind jetzt Objekte { fileName, uploadDateUtc, takenDateUtc }
        const frag = document.createDocumentFragment();
        for (const it of files) {
            const wrapper = document.createElement('div');
            wrapper.innerHTML = cardHtml(it).trim();
            frag.appendChild(wrapper.firstElementChild);
        }
        if (frag.childNodes.length) {
            // Bei upload_desc sind neue oben korrekt. Für andere Sortierungen bräuchte man Inserts an Position.
            gallery.insertBefore(frag, gallery.firstChild);
        }
    });

    // Delete asynchron
    document.addEventListener('submit', async (e) => {
        const form = e.target;
        if (!(form instanceof HTMLFormElement)) return;
        const action = form.getAttribute('action') || '';
        if (!action.includes('?handler=Delete')) return;
        e.preventDefault();

        const fd = new FormData(form);
        const res = await fetch(action, {
            method: 'POST',
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            body: fd
        });
        if (!res.ok) return;
        const json = await res.json().catch(() => ({}));
        const fileName = json.deleted || fd.get('fileName');
        const el = document.querySelector(`.gallery-card[data-fn="${CSS.escape(fileName)}"]`);
        if (el) el.remove();
    }, true);
})();
