namespace DesktopAiTestAgent.AgentRunner.Dashboard;

/// <summary>
/// The single-page dashboard UI, served verbatim by <see cref="DashboardServer"/>.
/// Vanilla JS, no build step, no external assets — it only calls the local JSON API.
/// </summary>
internal static class DashboardHtml
{
    public const string Page =
        """
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>AgentLoop Dashboard</title>
          <style>
            :root { color-scheme: dark; --bg:#0f1115; --panel:#171a21; --line:#262b36; --fg:#e6e9ef; --mut:#8b93a3; --acc:#5b9dff; --ok:#3fb950; --bad:#f85149; --warn:#d29922; }
            * { box-sizing: border-box; }
            body { margin:0; font:14px/1.5 system-ui,Segoe UI,Roboto,sans-serif; background:var(--bg); color:var(--fg); }
            header { display:flex; align-items:center; gap:16px; padding:12px 20px; border-bottom:1px solid var(--line); background:var(--panel); position:sticky; top:0; z-index:5; }
            header h1 { font-size:16px; margin:0; font-weight:600; }
            header .note { color:var(--warn); font-size:12px; }
            nav { display:flex; gap:6px; margin-left:auto; }
            nav button { background:transparent; color:var(--mut); border:1px solid transparent; padding:6px 12px; border-radius:6px; cursor:pointer; font-size:13px; }
            nav button.active { color:var(--fg); background:#222836; border-color:var(--line); }
            main { padding:20px; max-width:1200px; margin:0 auto; }
            .panel { background:var(--panel); border:1px solid var(--line); border-radius:10px; padding:16px; margin-bottom:16px; }
            .row { display:flex; gap:10px; flex-wrap:wrap; align-items:center; }
            .grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(280px,1fr)); gap:12px; }
            .card { background:#12151c; border:1px solid var(--line); border-radius:8px; padding:12px; }
            .card h4 { margin:0 0 6px; font-size:13px; }
            .mut { color:var(--mut); }
            .badge { display:inline-block; font-size:11px; padding:1px 7px; border-radius:20px; border:1px solid var(--line); color:var(--mut); }
            .b-ok { color:var(--ok); border-color:#2a4; } .b-bad { color:var(--bad); border-color:#a33; } .b-warn { color:var(--warn); border-color:#a82; }
            button.act { background:var(--acc); color:#08101f; border:none; padding:6px 12px; border-radius:6px; cursor:pointer; font-weight:600; }
            button.ghost { background:transparent; color:var(--fg); border:1px solid var(--line); padding:6px 12px; border-radius:6px; cursor:pointer; }
            input, select, textarea { width:100%; background:#0d1016; color:var(--fg); border:1px solid var(--line); border-radius:6px; padding:7px; font:inherit; }
            label { display:block; font-size:12px; color:var(--mut); margin:8px 0 3px; }
            table { width:100%; border-collapse:collapse; }
            th, td { text-align:left; padding:7px 8px; border-bottom:1px solid var(--line); font-size:13px; }
            tr.clk { cursor:pointer; } tr.clk:hover td { background:#1b212c; }
            h3 { margin:0 0 12px; font-size:14px; color:var(--mut); text-transform:uppercase; letter-spacing:.04em; }
            pre { background:#0a0d12; border:1px solid var(--line); border-radius:6px; padding:10px; overflow:auto; max-height:340px; font-size:12px; }
            .shots { display:grid; grid-template-columns:repeat(auto-fill,minmax(180px,1fr)); gap:8px; }
            .shots img { width:100%; border:1px solid var(--line); border-radius:6px; }
            .hidden { display:none; }
            .cat { margin-bottom:18px; } .cat h3 .badge { margin-left:8px; }
          </style>
        </head>
        <body>
          <header>
            <h1>AgentLoop Dashboard</h1>
            <span class="note" title="Localhost-only dev tool. Screenshots may show sensitive UI.">localhost-only · not for CI</span>
            <nav>
              <button data-tab="catalog" class="active">Catalog</button>
              <button data-tab="create">Create</button>
              <button data-tab="runs">Runs</button>
              <button data-tab="live">Live</button>
            </nav>
          </header>
          <main>
            <section id="tab-catalog"></section>
            <section id="tab-create" class="hidden"></section>
            <section id="tab-runs" class="hidden"></section>
            <section id="tab-live" class="hidden"></section>
          </main>

          <script>
            const $ = (s, r=document) => r.querySelector(s);
            const el = (t, p={}) => Object.assign(document.createElement(t), p);
            let CONFIG = { traceUiTemplate: "" };
            const esc = s => (s==null?"":String(s)).replace(/[&<>]/g, c=>({"&":"&amp;","<":"&lt;",">":"&gt;"}[c]));
            const resultClass = r => r==="Passed"||r==="Succeeded"?"b-ok":(r==="Aborted"||r==="Failed"||r==="Blocked"?"b-bad":"b-warn");

            async function api(path, opts) {
              const res = await fetch(path, opts);
              const ct = res.headers.get("content-type")||"";
              const data = ct.includes("json") ? await res.json() : await res.text();
              if (!res.ok) throw new Error((data && data.error) || res.statusText);
              return data;
            }

            // --- tabs ---
            document.querySelectorAll("nav button").forEach(b => b.onclick = () => show(b.dataset.tab));
            function show(tab) {
              document.querySelectorAll("nav button").forEach(b => b.classList.toggle("active", b.dataset.tab===tab));
              ["catalog","create","runs","live"].forEach(t => $("#tab-"+t).classList.toggle("hidden", t!==tab));
              if (tab==="catalog") loadCatalog();
              if (tab==="runs") loadRuns();
              if (tab==="live") loadLive();
            }

            // --- catalog ---
            async function loadCatalog() {
              const host = $("#tab-catalog");
              host.innerHTML = "<div class='panel'><h3>Test catalog</h3><div id='cat-body' class='mut'>Loading…</div></div>";
              try {
                const data = await api("/api/tests");
                const bySuite = {};
                data.tests.forEach(t => (bySuite[t.suite||"(no suite)"] ||= []).push(t));
                const body = $("#cat-body"); body.innerHTML = "";
                if (!data.count) { body.textContent = "No tests found under tests/."; return; }
                Object.keys(bySuite).sort().forEach(suite => {
                  const cat = el("div", {className:"cat"});
                  cat.innerHTML = `<h3>${esc(suite)} <span class="badge">${bySuite[suite].length}</span></h3>`;
                  const grid = el("div", {className:"grid"});
                  bySuite[suite].forEach(t => grid.appendChild(testCard(t)));
                  cat.appendChild(grid); body.appendChild(cat);
                });
              } catch(e) { $("#cat-body").innerHTML = `<span class='b-bad'>${esc(e.message)}</span>`; }
            }
            function testCard(t) {
              const c = el("div", {className:"card"});
              c.innerHTML = `<h4>${esc(t.id)}</h4>
                <div class="mut" style="margin-bottom:6px">${esc(t.title||"")}</div>
                <div class="row" style="margin-bottom:8px">
                  ${t.framework?`<span class="badge">${esc(t.framework)}</span>`:""}
                  ${t.priority?`<span class="badge">${esc(t.priority)}</span>`:""}
                  ${(t.tags||[]).map(x=>`<span class="badge">${esc(x)}</span>`).join("")}
                </div>
                <div class="mut" style="font-size:12px;margin-bottom:8px">${esc(t.goal||"")}</div>`;
              const btn = el("button", {className:"act", textContent:"▶ Run"});
              btn.onclick = () => launch(t);
              c.appendChild(btn);
              return c;
            }
            async function launch(t) {
              try {
                await api("/api/runs", {method:"POST", headers:{"content-type":"application/json"},
                  body: JSON.stringify({ planPath:t.planPath, testId:t.id, window:t.targetWindow })});
                show("live");
              } catch(e) { alert("Launch failed: " + e.message); }
            }

            // --- create ---
            function loadCreate() {
              $("#tab-create").innerHTML = `<div class="panel" style="max-width:640px">
                <h3>Create a test (ticket)</h3>
                <div class="mut" style="margin-bottom:10px">Writes a validated YAML under tests/created/. The YAML stays the source of truth.</div>
                <label>Test id *</label><input id="f-id" placeholder="DEMO-MYTEST-001"/>
                <label>Suite</label><input id="f-suite" placeholder="created"/>
                <label>Title</label><input id="f-title"/>
                <div class="row"><div style="flex:1"><label>Framework</label>
                  <select id="f-fw"><option value="">—</option><option>winforms</option><option>wpf</option><option>maui</option><option>avalonia</option></select></div>
                  <div style="flex:1"><label>Priority</label><select id="f-prio"><option value="">—</option><option>P1</option><option>P2</option><option>P3</option></select></div></div>
                <label>Target window</label><input id="f-win" placeholder="Sample Login App (.NET 8)"/>
                <label>Goal *</label><textarea id="f-goal" rows="2"></textarea>
                <label>Success condition (optional)</label><input id="f-success"/>
                <div class="row"><div style="flex:1"><label>Max steps</label><input id="f-steps" type="number" value="8"/></div>
                  <div style="flex:2"><label>Allowed actions (comma)</label><input id="f-actions" value="EnterText, Click, Assert, Done, Wait"/></div></div>
                <label>Tags (comma)</label><input id="f-tags"/>
                <div class="row" style="margin-top:12px"><button class="act" id="f-save">Validate &amp; save</button></div>
                <div id="f-out" style="margin-top:12px"></div>
              </div>`;
              $("#f-save").onclick = saveTest;
            }
            async function saveTest() {
              const list = s => s.split(",").map(x=>x.trim()).filter(Boolean);
              const req = {
                id:$("#f-id").value.trim(), suite:$("#f-suite").value.trim(), title:$("#f-title").value.trim(),
                framework:$("#f-fw").value, priority:$("#f-prio").value, targetWindow:$("#f-win").value.trim(),
                goal:$("#f-goal").value.trim(), successCondition:$("#f-success").value.trim()||null,
                maxSteps:parseInt($("#f-steps").value)||8, allowedActions:list($("#f-actions").value), tags:list($("#f-tags").value)
              };
              const out = $("#f-out");
              try {
                const r = await api("/api/tests", {method:"POST", headers:{"content-type":"application/json"}, body:JSON.stringify(req)});
                out.innerHTML = `<span class="b-ok">Saved ${esc(r.planPath)}</span><pre>${esc(r.yaml)}</pre>`;
              } catch(e) { out.innerHTML = `<span class="b-bad">${esc(e.message)}</span>`; }
            }

            // --- runs ---
            async function loadRuns() {
              const host = $("#tab-runs");
              host.innerHTML = "<div class='panel'><h3>Run history</h3><div id='runs-body' class='mut'>Loading…</div></div><div id='run-detail'></div>";
              try {
                const data = await api("/api/runs");
                if (!data.count) { $("#runs-body").textContent = "No runs yet."; return; }
                const t = el("table");
                t.innerHTML = "<thead><tr><th>Result</th><th>Test</th><th>Framework</th><th>Score</th><th>Steps</th><th>Started</th></tr></thead>";
                const tb = el("tbody");
                data.runs.forEach(r => {
                  const tr = el("tr", {className:"clk"});
                  tr.innerHTML = `<td><span class="badge ${resultClass(r.result)}">${esc(r.result)}</span></td>
                    <td>${esc(r.testId||r.runId)}</td><td>${esc(r.framework||"")}</td><td>${r.finalScore}</td>
                    <td>${r.steps}</td><td class="mut">${esc((r.startedAt||"").replace("T"," ").slice(0,19))}</td>`;
                  tr.onclick = () => showRun(r.runId);
                  tb.appendChild(tr);
                });
                t.appendChild(tb); $("#runs-body").innerHTML=""; $("#runs-body").appendChild(t);
              } catch(e) { $("#runs-body").innerHTML = `<span class='b-bad'>${esc(e.message)}</span>`; }
            }
            async function showRun(runId) {
              const host = $("#run-detail");
              host.innerHTML = "<div class='panel'>Loading run…</div>";
              try {
                const r = await api("/api/runs/"+runId);
                const trace = r.traceId ? (CONFIG.traceUiTemplate
                  ? `<a href="${esc(CONFIG.traceUiTemplate.replace("{traceId}", r.traceId))}" target="_blank">${esc(r.traceId)}</a>`
                  : `<code>${esc(r.traceId)}</code> <span class="mut">(open in your Aspire dashboard → Traces)</span>`) : "<span class='mut'>none</span>";
                const steps = (r.steps||[]).map(s => `<tr><td>${s.stepNumber}</td><td>${esc(s.actionType)}</td><td>${esc(s.actionTarget||"")}</td>
                  <td><span class="badge ${s.outcome==="Succeeded"?"b-ok":"b-bad"}">${esc(s.outcome)}</span></td>
                  <td>${esc(s.failureCode||s.guardCode||"")}</td><td>${s.cumulativeScore}</td></tr>`).join("");
                host.innerHTML = `<div class="panel">
                  <h3>Run ${esc(r.runId)} <span class="badge ${resultClass(r.result)}">${esc(r.result)}</span></h3>
                  <div class="mut" style="margin-bottom:8px">${esc(r.goalDescription||"")}</div>
                  <div class="row" style="margin-bottom:10px"><span>Trace: ${trace}</span></div>
                  <table><thead><tr><th>#</th><th>Action</th><th>Target</th><th>Outcome</th><th>Failure/Guard</th><th>Score</th></tr></thead><tbody>${steps}</tbody></table>
                  <h3 style="margin-top:16px">Screenshots</h3><div class="shots" id="run-shots" class="mut">…</div>
                </div>`;
                loadShots(runId, $("#run-shots"));
              } catch(e) { host.innerHTML = `<div class='panel b-bad'>${esc(e.message)}</div>`; }
            }
            async function loadShots(runId, target) {
              try {
                const d = await api("/api/runs/"+runId+"/screenshots");
                if (!d.screenshots.length) { target.innerHTML = "<span class='mut'>No screenshots (evidence level may be minimal).</span>"; return; }
                target.innerHTML = "";
                d.screenshots.forEach(f => target.appendChild(el("img", {src:`/api/screenshot?run=${encodeURIComponent(runId)}&file=${encodeURIComponent(f)}`, loading:"lazy", title:f})));
              } catch(e) { target.innerHTML = `<span class='b-bad'>${esc(e.message)}</span>`; }
            }

            // --- live ---
            let liveTimer = null;
            async function loadLive() {
              clearInterval(liveTimer);
              const host = $("#tab-live");
              host.innerHTML = "<div class='panel'><h3>Live runs</h3><div class='mut'>Launched runs spawn the CLI; they need your target app + .env. Status updates every 2s.</div><div id='live-body' style='margin-top:10px'></div></div>";
              const render = async () => {
                try {
                  const d = await api("/api/jobs");
                  const body = $("#live-body");
                  if (!d.jobs.length) { body.innerHTML = "<span class='mut'>No active jobs. Launch one from the Catalog.</span>"; return; }
                  body.innerHTML = "";
                  d.jobs.forEach(j => {
                    const card = el("div", {className:"card"});
                    const cls = j.status==="running"?"b-warn":(j.exitCode===0?"b-ok":"b-bad");
                    const tail = (j.logs||[]).slice(-14).map(esc).join("\n");
                    card.innerHTML = `<div class="row"><b>${esc(j.testId)}</b>
                      <span class="badge ${cls}">${esc(j.status)}${j.exitCode!=null?(" · exit "+j.exitCode):""}</span>
                      ${j.runId?`<span class="badge">run ${esc(j.runId)}</span>`:""}</div>
                      <pre>${tail||"(starting…)"}</pre>
                      <div class="shots" id="shots-${esc(j.jobId)}"></div>`;
                    body.appendChild(card);
                    if (j.runId) loadShots(j.runId, $("#shots-"+j.jobId));
                  });
                } catch(e) { $("#live-body").innerHTML = `<span class='b-bad'>${esc(e.message)}</span>`; }
              };
              await render();
              liveTimer = setInterval(render, 2000);
            }

            (async () => {
              try { CONFIG = await api("/api/config"); } catch {}
              loadCreate(); loadCatalog();
            })();
          </script>
        </body>
        </html>
        """;
}
