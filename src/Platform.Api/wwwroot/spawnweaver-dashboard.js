// Dashboard helpers (static SSR pages). Loaded on every /dashboard page.

function swCopy(text, btn) {
    navigator.clipboard.writeText(text).then(function () {
        if (!btn) return;
        var original = btn.textContent;
        btn.textContent = 'Copied';
        setTimeout(function () { btn.textContent = original; }, 1200);
    });
}

function swKeyRow(label, value, secret) {
    var row = document.createElement('div');
    row.className = 'key-row' + (secret ? ' secret' : '');

    var lab = document.createElement('div');
    lab.className = 'key-label';
    lab.textContent = label;
    row.appendChild(lab);

    var box = document.createElement('div');
    box.className = 'key-box';

    var val = document.createElement('span');
    val.className = 'key-value';
    val.textContent = value;
    box.appendChild(val);

    var btn = document.createElement('button');
    btn.className = 'copy-btn';
    btn.textContent = 'Copy';
    btn.onclick = function () { swCopy(value, btn); };
    box.appendChild(btn);

    row.appendChild(box);
    return row;
}

function swSetStatus(el, message, ok) {
    if (!el) return;
    el.style.color = ok ? '#54d6a0' : '#ff7a90';
    el.textContent = message;
}

// Renders project keys + (optionally) the recommended setup plan into a container.
function swRenderProjectResult(resultEl, p) {
    resultEl.innerHTML = '';

    var warn = document.createElement('div');
    warn.className = 'banner banner-warn';
    warn.textContent = '⚠ Save the secret key now — it is shown only once and cannot be retrieved again.';
    resultEl.appendChild(warn);

    var head = document.createElement('div');
    head.className = 'section-title';
    head.textContent = 'Project “' + p.name + '” created';
    resultEl.appendChild(head);

    resultEl.appendChild(swKeyRow('Project ID', p.id, false));
    resultEl.appendChild(swKeyRow('Public key (safe to ship in your game)', p.publicKey, false));
    resultEl.appendChild(swKeyRow('Secret key (server-side only — store securely)', p.secretKey, true));

    var snipLabel = document.createElement('div');
    snipLabel.className = 'key-label';
    snipLabel.style.margin = '14px 0 5px';
    snipLabel.textContent = 'Connect from Godot';
    resultEl.appendChild(snipLabel);

    var pre = document.createElement('pre');
    pre.textContent =
        'MultiplayerService.configure("' + p.publicKey + '")\n' +
        'MultiplayerService.connect_to_server("wss://your-domain/connect")';
    resultEl.appendChild(pre);

    var manage = document.createElement('p');
    manage.style.marginBottom = '0';
    manage.innerHTML = '<a class="btn btn-secondary btn-sm" href="/dashboard/projects/' + p.id + '">Manage project &amp; keys →</a>';
    resultEl.appendChild(manage);

    resultEl.classList.remove('hidden');
    resultEl.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
}

// Create a project from a name (+ optional environment select), then render the keys.
async function swCreateProjectFrom(nameId, envId, statusId, resultId) {
    var nameInput = document.getElementById(nameId);
    var statusEl = document.getElementById(statusId);
    var resultEl = document.getElementById(resultId);
    var name = (nameInput.value || '').trim();

    statusEl.textContent = '';
    if (!name) {
        swSetStatus(statusEl, 'Enter a project name.', false);
        return;
    }

    var body = { name: name };
    if (envId) {
        var envEl = document.getElementById(envId);
        if (envEl) { body.environment = envEl.value; }
    }

    swSetStatus(statusEl, 'Creating…', true);
    try {
        var res = await fetch('/api/projects', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });
        if (!res.ok) {
            swSetStatus(statusEl, res.status === 401 ? 'Please sign in to create a project.'
                : 'Could not create project (' + res.status + ').', false);
            return;
        }
        var p = await res.json();
        statusEl.textContent = '';
        nameInput.value = '';
        var form = document.getElementById('ob-form');
        if (form) { form.classList.add('hidden'); }
        swRenderProjectResult(resultEl, p);
    } catch (e) {
        swSetStatus(statusEl, 'Could not reach the server.', false);
    }
}

function swCreateProject() { swCreateProjectFrom('cp-name', null, 'cp-status', 'cp-result'); }
function swCreateProjectOnboarding() { swCreateProjectFrom('ob-name', 'ob-env', 'ob-status', 'ob-result'); }

// Project details — regenerate the secret key (revealed once).
async function swRotateSecret(projectId) {
    var statusEl = document.getElementById('pk-status');
    var resultEl = document.getElementById('pk-secret-result');
    if (!confirm('Generate a new secret key? The current secret stops working immediately.')) { return; }
    swSetStatus(statusEl, 'Generating…', true);
    try {
        var res = await fetch('/api/projects/' + projectId + '/keys/secret', { method: 'POST' });
        if (!res.ok) { swSetStatus(statusEl, 'Could not generate key (' + res.status + ').', false); return; }
        var data = await res.json();
        statusEl.textContent = '';
        resultEl.innerHTML = '';
        var warn = document.createElement('div');
        warn.className = 'banner banner-warn';
        warn.textContent = '⚠ Save this secret now — it is shown only once.';
        resultEl.appendChild(warn);
        resultEl.appendChild(swKeyRow('New secret key', data.secretKey, true));
        resultEl.classList.remove('hidden');
    } catch (e) { swSetStatus(statusEl, 'Could not reach the server.', false); }
}

// Project details — rotate the public key (disconnects shipped clients on the old key).
async function swRotatePublic(projectId) {
    var statusEl = document.getElementById('pk-status');
    var resultEl = document.getElementById('pk-public-result');
    if (!confirm('Rotate the public key? Any game already shipped with the old key will stop connecting.')) { return; }
    swSetStatus(statusEl, 'Rotating…', true);
    try {
        var res = await fetch('/api/projects/' + projectId + '/keys/public', { method: 'POST' });
        if (!res.ok) { swSetStatus(statusEl, 'Could not rotate key (' + res.status + ').', false); return; }
        var data = await res.json();
        statusEl.textContent = '';
        resultEl.innerHTML = '';
        resultEl.appendChild(swKeyRow('New public key', data.publicKey, false));
        resultEl.classList.remove('hidden');
    } catch (e) { swSetStatus(statusEl, 'Could not reach the server.', false); }
}

// Auth.
async function swAuth(url, body, statusEl) {
    swSetStatus(statusEl, 'Please wait…', true);
    try {
        var res = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });
        var data = await res.json().catch(function () { return {}; });
        if (!res.ok) {
            if (data && data.code === 'email_not_verified') {
                swShowVerifyNeeded(statusEl, data.email || body.email);
                return;
            }
            var msg = swFirstError(data) || 'Something went wrong (' + res.status + ').';
            swSetStatus(statusEl, msg, false);
            return;
        }
        window.location.href = data.redirect || '/dashboard';
    } catch (e) {
        swSetStatus(statusEl, 'Could not reach the server.', false);
    }
}

// Sign-in was refused because the email isn't verified: explain and offer a resend.
function swShowVerifyNeeded(statusEl, email) {
    statusEl.style.color = '';
    statusEl.textContent = 'Verify your email to sign in — check your inbox for the confirmation link. ';
    var btn = document.createElement('a');
    btn.href = '#';
    btn.textContent = 'Resend it';
    btn.onclick = function (e) { e.preventDefault(); swResendVerification(email, statusEl); };
    statusEl.appendChild(btn);
}

// Re-send a verification email. Always reports success (server avoids account enumeration).
async function swResendVerification(email, statusEl) {
    email = (email || '').trim();
    if (!email) { swSetStatus(statusEl, 'Enter your email first.', false); return; }
    swSetStatus(statusEl, 'Sending…', true);
    try {
        var res = await fetch('/api/auth/verify/resend', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email: email })
        });
        var data = await res.json().catch(function () { return {}; });
        statusEl.style.color = '#54d6a0';
        statusEl.textContent = data.message || 'If that account still needs verification, we’ve sent a new link.';
    } catch (e) {
        swSetStatus(statusEl, 'Could not reach the server.', false);
    }
}

function swFirstError(data) {
    if (!data) return null;
    // ValidationProblem / our Problem dictionary: { field: [msg] }
    for (var key in data) {
        if (Array.isArray(data[key]) && data[key].length) return data[key][0];
    }
    if (data.errors) {
        for (var k in data.errors) {
            if (Array.isArray(data.errors[k]) && data.errors[k].length) return data.errors[k][0];
        }
    }
    return null;
}

// Passwordless magic-link sign-in.
async function swMagicLink(emailId, statusId) {
    var email = (document.getElementById(emailId).value || '').trim();
    var status = document.getElementById(statusId);
    if (!email) { swSetStatus(status, 'Enter your email first.', false); return; }
    swSetStatus(status, 'Sending…', true);
    try {
        var res = await fetch('/api/auth/magic/request', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email: email })
        });
        var data = await res.json().catch(function () { return {}; });
        if (!res.ok) {
            swSetStatus(status, data.message || 'Could not send a link.', false);
            return;
        }
        status.style.color = '#54d6a0';
        status.textContent = data.message || 'Check your email for a sign-in link.';
        if (data.devLink) {
            var a = document.createElement('a');
            a.href = data.devLink;
            a.textContent = ' Open link (dev)';
            status.appendChild(a);
        }
    } catch (e) {
        swSetStatus(status, 'Could not reach the server.', false);
    }
}

function swSignIn() {
    swAuth('/api/auth/signin', {
        email: (document.getElementById('si-email').value || '').trim(),
        password: document.getElementById('si-password').value || ''
    }, document.getElementById('si-status'));
}

function swSignUp() {
    swAuth('/api/auth/signup', {
        email: (document.getElementById('su-email').value || '').trim(),
        displayName: (document.getElementById('su-name').value || '').trim(),
        password: document.getElementById('su-password').value || ''
    }, document.getElementById('su-status'));
}

async function swSignOut() {
    try {
        var res = await fetch('/api/auth/signout', { method: 'POST' });
        var data = await res.json().catch(function () { return {}; });
        window.location.href = data.redirect || '/dashboard/signin';
    } catch (e) {
        window.location.href = '/dashboard/signin';
    }
}

// Account settings.
async function swSaveDisplayName() {
    var statusEl = document.getElementById('ac-name-status');
    var name = (document.getElementById('ac-name').value || '').trim();
    swSetStatus(statusEl, 'Saving…', true);
    try {
        var res = await fetch('/api/account', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ displayName: name })
        });
        if (!res.ok) {
            var data = await res.json().catch(function () { return {}; });
            swSetStatus(statusEl, swFirstError(data) || 'Could not save.', false);
            return;
        }
        swSetStatus(statusEl, 'Saved.', true);
        setTimeout(function () { window.location.reload(); }, 600);
    } catch (e) {
        swSetStatus(statusEl, 'Could not reach the server.', false);
    }
}

async function swChangePassword() {
    var statusEl = document.getElementById('ac-pw-status');
    var body = {
        currentPassword: document.getElementById('ac-cur').value || '',
        newPassword: document.getElementById('ac-new').value || ''
    };
    swSetStatus(statusEl, 'Updating…', true);
    try {
        var res = await fetch('/api/account/password', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });
        if (!res.ok) {
            var data = await res.json().catch(function () { return {}; });
            swSetStatus(statusEl, swFirstError(data) || 'Could not update password.', false);
            return;
        }
        document.getElementById('ac-cur').value = '';
        document.getElementById('ac-new').value = '';
        swSetStatus(statusEl, 'Password updated.', true);
    } catch (e) {
        swSetStatus(statusEl, 'Could not reach the server.', false);
    }
}

// Debug bundle viewer (Milestone 22.9) — parse & render an SDK create_debug_report().
function swClearDebugBundle() {
    document.getElementById('db-input').value = '';
    document.getElementById('db-status').textContent = '';
    var result = document.getElementById('db-result');
    result.innerHTML = '';
    result.classList.add('hidden');
}

function swViewDebugBundle() {
    var statusEl = document.getElementById('db-status');
    var resultEl = document.getElementById('db-result');
    var raw = (document.getElementById('db-input').value || '').trim();
    if (!raw) {
        swSetStatus(statusEl, 'Paste a debug report first.', false);
        return;
    }

    var report;
    try {
        report = JSON.parse(raw);
    } catch (e) {
        swSetStatus(statusEl, 'That is not valid JSON.', false);
        return;
    }

    statusEl.textContent = '';
    resultEl.innerHTML = '';

    // Overview fields.
    var ping = report.ping || {};
    var overview = [
        ['SDK version', report.sdk_version],
        ['Godot version', report.godot_version],
        ['Connection state', report.connection_state],
        ['Player ID', report.player_id],
        ['Connection ID', report.connection_id],
        ['Room', (report.room_id || '—') + (report.room_kind ? ' (' + report.room_kind + ')' : '')],
        ['Auto reconnect', String(report.auto_reconnect)],
        ['Last disconnect', report.last_disconnect_reason || '—'],
        ['Ping (last / avg ms)', (ping.last_ms != null ? ping.last_ms : '—') + ' / ' + (ping.avg_ms != null ? Math.round(ping.avg_ms) : '—')]
    ];
    resultEl.appendChild(swCard('Overview', swKvTable(overview)));

    // Last errors.
    var errors = report.last_errors || [];
    if (errors.length) {
        var rows = errors.map(function (e) { return [e.code || '', e.message || '']; });
        resultEl.appendChild(swCard('Last errors (' + errors.length + ')', swTable(['Code', 'Message'], rows)));
    }

    // Recent messages.
    var messages = report.recent_messages || [];
    if (messages.length) {
        var mrows = messages.map(function (m) {
            return [(m.dir || ''), (m.type || ''), (m.t != null ? String(m.t) : '')];
        });
        resultEl.appendChild(swCard('Recent messages (' + messages.length + ')', swTable(['Dir', 'Type', 't (ms)'], mrows)));
    }

    resultEl.classList.remove('hidden');
}

function swCard(title, bodyNode) {
    var card = document.createElement('div');
    card.className = 'card';
    var head = document.createElement('div');
    head.className = 'section-title';
    head.textContent = title;
    card.appendChild(head);
    card.appendChild(bodyNode);
    return card;
}

function swKvTable(pairs) {
    var grid = document.createElement('div');
    grid.className = 'kv';
    pairs.forEach(function (p) {
        var label = document.createElement('div');
        label.className = 'kv-label';
        label.textContent = p[0];
        var value = document.createElement('div');
        value.className = 'mono';
        value.textContent = (p[1] == null || p[1] === '') ? '—' : String(p[1]);
        grid.appendChild(label);
        grid.appendChild(value);
    });
    return grid;
}

function swTable(headers, rows) {
    var table = document.createElement('table');
    var thead = document.createElement('thead');
    var htr = document.createElement('tr');
    headers.forEach(function (h) { var th = document.createElement('th'); th.textContent = h; htr.appendChild(th); });
    thead.appendChild(htr);
    table.appendChild(thead);
    var tbody = document.createElement('tbody');
    rows.forEach(function (r) {
        var tr = document.createElement('tr');
        r.forEach(function (c) { var td = document.createElement('td'); td.className = 'mono'; td.textContent = c; tr.appendChild(td); });
        tbody.appendChild(tr);
    });
    table.appendChild(tbody);
    return table;
}

async function swRevokeAllSessions() {
    var statusEl = document.getElementById('ac-sessions-status');
    swSetStatus(statusEl, 'Signing out other devices…', true);
    try {
        var res = await fetch('/api/account/sessions/revoke-all', { method: 'POST' });
        if (!res.ok) {
            swSetStatus(statusEl, 'Could not revoke sessions.', false);
            return;
        }
        setTimeout(function () { window.location.reload(); }, 600);
    } catch (e) {
        swSetStatus(statusEl, 'Could not reach the server.', false);
    }
}
