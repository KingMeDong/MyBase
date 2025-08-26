(function () {
    const gallery = document.getElementById('gallery');
    if (!gallery) return;

    let page = 1;
    const pageSize = parseInt(gallery.dataset.pagesize || "60", 10);
    let loading = false;
    let hasMore = true;

    // Anti-forgery für Delete-Formulare
    function getToken() {
        const el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : null;
    }

    function cardHtml(fileName) {
        const enc = encodeURIComponent(fileName);
        const thumbUrl = `/thumbs/${enc}?w=320`;
        const fullUrl = `/media/images/${enc}`;
        const token = getToken() || "";
        return `
      <div class="gallery-card" data-fn="${fileName}">
        <a href="${fullUrl}" target="_blank" rel="noopener">
          <img src="${thumbUrl}" alt="${fileName}" loading="lazy" />
        </a>
        <div class="gallery-meta">
          <span class="file-name">${fileName}</span>
          <div class="actions">
            <a class="icon-btn" title="Download" href="/download/${enc}?scope=images" download>⬇️</a>
            <form method="post" action="?handler=Delete" onsubmit="return confirm('Bild wirklich löschen?');">
              <input type="hidden" name="__RequestVerificationToken" value="${token}">
              <input type="hidden" name="fileName" value="${fileName}">
              <button type="submit" class="icon-btn danger" title="Löschen">🗑️</button>
            </form>
          </div>
        </div>
      </div>`;
    }

    async function loadNext() {
        if (loading || !hasMore) return;
        loading = true;
        page += 1;

        try {
            const res = await fetch(`/api/media/images?page=${page}&pageSize=${pageSize}`);
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const data = await res.json();
            hasMore = data.hasMore;

            const frag = document.createDocumentFragment();
            for (const fn of data.items) {
                const div = document.createElement('div');
                div.innerHTML = cardHtml(fn).trim();
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

    // IntersectionObserver für automatisches Nachladen
    const sentinel = document.getElementById('gallery-sentinel');
    if (sentinel) {
        const io = new IntersectionObserver((entries) => {
            entries.forEach(e => {
                if (e.isIntersecting) loadNext();
            });
        }, { rootMargin: '600px 0px' }); // frühzeitig laden
        io.observe(sentinel);
    }
})();
