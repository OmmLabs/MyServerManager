(() => {
    let operationPending = false;
    const disabledForOperation = new Set();

    document.addEventListener('submit', event => {
        const form = event.target;
        if (!form.matches('[data-operation]')) return;
        if (operationPending || (form.dataset.confirm && !window.confirm(form.dataset.confirm))) {
            event.preventDefault();
            return;
        }

        const confirmation = form.elements.namedItem('confirmed');
        if (confirmation) confirmation.value = 'true';
        operationPending = true;
        for (const button of document.querySelectorAll('[data-operation] button[type=submit]:enabled')) {
            disabledForOperation.add(button);
            button.disabled = true;
        }
    });

    window.addEventListener('pageshow', () => {
        operationPending = false;
        for (const button of disabledForOperation) button.disabled = false;
        disabledForOperation.clear();
    });

    const refreshers = new Map();
    const refreshStatus = document.querySelector('[data-refresh-status]');
    for (const region of document.querySelectorAll('[data-poll-url]')) {
        let loading = false;
        const isFocused = () => region.contains(document.activeElement);

        async function refresh(manual = false) {
            if (loading || operationPending || (!manual && (document.hidden || isFocused()))) return;
            loading = true;
            if (manual) region.setAttribute('aria-busy', 'true');
            try {
                const url = new URL(region.dataset.pollUrl, window.location.href);
                if (manual) url.searchParams.set('refresh', 'true');
                const response = await fetch(url, { cache: 'no-store', signal: AbortSignal.timeout(12000) });
                if (response.redirected) {
                    window.location.reload();
                    return;
                }
                if (!response.ok) throw new Error('Refresh failed');
                const html = await response.text();
                if (!operationPending && (manual || !isFocused())) region.innerHTML = html;
                if (refreshStatus) refreshStatus.textContent = '';
            } catch {
                if (refreshStatus) refreshStatus.textContent = 'Refresh failed. Values may be out of date.';
            } finally {
                loading = false;
                region.removeAttribute('aria-busy');
            }
        }

        refreshers.set(region.id, refresh);
        const interval = Number(region.dataset.pollMs);
        if (interval > 0) {
            const poll = async () => {
                await refresh();
                window.setTimeout(poll, interval);
            };
            window.setTimeout(poll, interval);
        }
    }

    for (const button of document.querySelectorAll('[data-refresh]')) {
        button.addEventListener('click', async () => {
            button.disabled = true;
            try {
                await Promise.all(button.dataset.refresh.split(' ').map(id => refreshers.get(id)?.(true)));
            } finally { button.disabled = false; }
        });
    }

    const logFilters = document.querySelector('[data-log-filters]');
    if (logFilters) {
        const level = logFilters.querySelector('[data-log-level]');
        const search = logFilters.querySelector('[data-log-search]');
        const count = logFilters.querySelector('[data-log-count]');
        const rows = Array.from(document.querySelectorAll('[data-log-entry]'), row => ({ row, text: row.textContent.toLowerCase() }));
        const filter = () => {
            const query = search.value.trim().toLowerCase();
            let visible = 0;
            for (const { row, text } of rows) {
                row.hidden = (level.value !== '' && row.dataset.logEntry !== level.value) || !text.includes(query);
                if (!row.hidden) visible++;
            }
            count.textContent = visible;
        };
        level.addEventListener('change', filter);
        search.addEventListener('input', filter);
        filter();
    }
})();
