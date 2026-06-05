namespace DesktopAiTestAgent.AgentRunner.Dashboard;

/// <summary>
/// The single-page dashboard UI, served verbatim by <see cref="DashboardServer"/>.
/// Vanilla JS, no build step, no external assets (offline / localhost) — it only calls
/// the local JSON API. Aesthetic: a "mission-control" telemetry console (monospace-
/// forward, status LEDs, a supervision-grade live view).
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
            :root {
              color-scheme: dark;
              --bg:#080a0f; --bg2:#0c0f16; --panel:#10141d; --panel2:#0b0e15;
              --line:#1c2330; --line2:#2a3445; --fg:#dde3ee; --mut:#727e94; --dim:#4a5468;
              --sig:#46e08a; --sig-dim:#1d7a48; --run:#f5b942; --bad:#ff5d6c; --info:#5aa9ff; --viol:#b98cff;
              --mono:"Cascadia Code","Cascadia Mono","JetBrains Mono",Consolas,ui-monospace,monospace;
              --sans:"Segoe UI Variable Display","Segoe UI",system-ui,sans-serif;
            }
            * { box-sizing:border-box; }
            html,body { height:100%; }
            body {
              margin:0; background:var(--bg); color:var(--fg); font-family:var(--mono); font-size:13px; line-height:1.5;
              background-image:
                linear-gradient(var(--bg2) 1px, transparent 1px),
                linear-gradient(90deg, var(--bg2) 1px, transparent 1px);
              background-size:40px 40px; background-position:-1px -1px;
            }
            ::-webkit-scrollbar { width:10px; height:10px; }
            ::-webkit-scrollbar-thumb { background:var(--line2); border-radius:6px; }
            ::-webkit-scrollbar-track { background:transparent; }

            /* shell */
            .shell { display:grid; grid-template-columns:208px 1fr; grid-template-rows:48px 1fr; height:100vh; }
            .topbar { grid-column:1/3; display:flex; align-items:center; gap:18px; padding:0 18px;
              border-bottom:1px solid var(--line); background:linear-gradient(180deg,#0e1219,#0a0d13); }
            .brand { font-weight:700; letter-spacing:.14em; font-size:12px; text-transform:uppercase; }
            .brand b { color:var(--sig); }
            .clock { color:var(--mut); font-size:12px; letter-spacing:.06em; }
            .stat-strip { display:flex; gap:14px; margin-left:auto; align-items:center; }
            .stat { display:flex; align-items:center; gap:6px; font-size:12px; color:var(--mut); }
            .stat b { color:var(--fg); font-variant-numeric:tabular-nums; }
            .dot { width:8px; height:8px; border-radius:50%; background:var(--dim); box-shadow:0 0 0 0 transparent; }
            .dot.run { background:var(--run); animation:pulse 1.4s infinite; }
            .dot.ok { background:var(--sig); } .dot.bad { background:var(--bad); }
            @keyframes pulse { 0%{box-shadow:0 0 0 0 rgba(245,185,66,.5);} 70%{box-shadow:0 0 0 7px rgba(245,185,66,0);} 100%{box-shadow:0 0 0 0 rgba(245,185,66,0);} }
            .env-tag { font-size:10.5px; letter-spacing:.1em; text-transform:uppercase; color:var(--run);
              border:1px solid #3a2f12; background:#1a1407; padding:2px 8px; border-radius:4px; }

            /* rail */
            .rail { border-right:1px solid var(--line); background:var(--panel2); padding:14px 10px; display:flex; flex-direction:column; gap:4px; }
            .rail button { display:flex; align-items:center; gap:10px; width:100%; text-align:left; background:transparent;
              color:var(--mut); border:1px solid transparent; border-radius:7px; padding:9px 11px; cursor:pointer;
              font-family:var(--mono); font-size:13px; letter-spacing:.02em; transition:.12s; }
            .rail button:hover { color:var(--fg); background:#141a24; }
            .rail button.active { color:var(--fg); background:#151c28; border-color:var(--line2); box-shadow:inset 2px 0 0 var(--sig); }
            .rail .ico { width:16px; text-align:center; color:var(--sig); opacity:.85; }
            .rail .spacer { flex:1; }
            .rail .hint { color:var(--dim); font-size:10.5px; line-height:1.45; padding:8px 11px; border-top:1px solid var(--line); }

            main { overflow:auto; padding:22px 26px; }
            .head { display:flex; align-items:baseline; gap:12px; margin:2px 0 18px; }
            .head h2 { margin:0; font-size:15px; letter-spacing:.08em; text-transform:uppercase; font-weight:700; }
            .head .sub { color:var(--mut); font-size:12px; }

            .panel { background:linear-gradient(180deg,var(--panel),var(--panel2)); border:1px solid var(--line); border-radius:10px; }
            .pad { padding:16px; }
            .mut { color:var(--mut); } .dim { color:var(--dim); }
            .grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(300px,1fr)); gap:12px; }
            .row { display:flex; gap:10px; flex-wrap:wrap; align-items:center; }

            .card { background:var(--panel); border:1px solid var(--line); border-radius:9px; padding:13px 14px; transition:.12s; }
            .card:hover { border-color:var(--line2); }
            .card h4 { margin:0 0 2px; font-size:13px; letter-spacing:.02em; }

            .chip { display:inline-flex; align-items:center; gap:5px; font-size:10.5px; letter-spacing:.04em; text-transform:uppercase;
              padding:2px 8px; border-radius:5px; border:1px solid var(--line2); color:var(--mut); background:#0e131c; white-space:nowrap; }
            .chip.ok { color:var(--sig); border-color:var(--sig-dim); background:#08160e; }
            .chip.bad { color:var(--bad); border-color:#5a2027; background:#190a0c; }
            .chip.run { color:var(--run); border-color:#4a3a12; background:#15110633; }
            .chip.info { color:var(--info); border-color:#23456e; }
            .fw { color:var(--viol); }

            button.act { background:var(--sig); color:#04140a; border:none; padding:7px 13px; border-radius:6px; cursor:pointer;
              font-family:var(--mono); font-weight:700; letter-spacing:.03em; font-size:12.5px; transition:.12s; }
            button.act:hover { filter:brightness(1.08); box-shadow:0 0 14px rgba(70,224,138,.25); }
            button.ghost { background:#0e131c; color:var(--fg); border:1px solid var(--line2); padding:7px 13px; border-radius:6px; cursor:pointer; font-family:var(--mono); font-size:12.5px; }
            button.ghost:hover { border-color:var(--sig-dim); }

            /* form */
            .form { max-width:680px; }
            .field { margin-bottom:14px; }
            .field > label { display:block; font-size:11px; letter-spacing:.08em; text-transform:uppercase; color:var(--fg); margin-bottom:2px; }
            .field .req { color:var(--sig); }
            .field .help { display:block; font-size:11.5px; color:var(--mut); margin-bottom:6px; }
            input, select, textarea { width:100%; background:#080b11; color:var(--fg); border:1px solid var(--line2); border-radius:6px;
              padding:8px 10px; font-family:var(--mono); font-size:13px; }
            input:focus, select, textarea:focus { outline:none; border-color:var(--sig-dim); box-shadow:0 0 0 2px rgba(70,224,138,.12); }
            input::placeholder, textarea::placeholder { color:var(--dim); }
            input[type=checkbox] { width:auto; box-shadow:none; vertical-align:middle; }
            #cat-bar input, #cat-bar select { width:auto; }
            #cat-bar #flt-q { flex:1; min-width:200px; }
            .two { display:grid; grid-template-columns:1fr 1fr; gap:12px; }

            table { width:100%; border-collapse:collapse; }
            th { text-align:left; padding:8px 10px; font-size:10.5px; letter-spacing:.08em; text-transform:uppercase; color:var(--mut); border-bottom:1px solid var(--line2); }
            td { padding:9px 10px; border-bottom:1px solid var(--line); font-size:12.5px; }
            tr.clk { cursor:pointer; } tr.clk:hover td { background:#141a24; }

            pre.term { background:#05070b; border:1px solid var(--line); border-radius:7px; padding:10px 12px; margin:0;
              overflow:auto; max-height:300px; font-size:11.5px; line-height:1.55; }
            .ln { white-space:pre-wrap; word-break:break-word; }
            .ln .lvl { display:inline-block; min-width:64px; color:var(--dim); }
            .l-INFO .lvl{color:var(--info);} .l-WARN .lvl{color:var(--run);} .l-ERROR{color:var(--bad);} .l-ERROR .lvl{color:var(--bad);}
            .l-DECISION .lvl{color:var(--viol);} .l-ACTION .lvl{color:var(--sig);} .l-SCORE .lvl{color:var(--mut);}

            .shots { display:flex; gap:8px; overflow-x:auto; padding-bottom:4px; }
            .shots img { height:128px; border:1px solid var(--line2); border-radius:6px; flex:0 0 auto; background:#05070b; }
            .shots.grid2 { display:grid; grid-template-columns:repeat(auto-fill,minmax(190px,1fr)); }
            .shots.grid2 img { height:auto; width:100%; }

            .progress { height:5px; background:#0c1019; border-radius:3px; overflow:hidden; border:1px solid var(--line); }
            .progress > i { display:block; height:100%; background:linear-gradient(90deg,var(--sig-dim),var(--sig)); transition:width .4s; }

            .live-card { border-left:3px solid var(--line2); }
            .live-card.running { border-left-color:var(--run); }
            .live-card.ok { border-left-color:var(--sig); }
            .live-card.bad { border-left-color:var(--bad); }
            .timer { font-variant-numeric:tabular-nums; color:var(--fg); }
            .empty { color:var(--dim); border:1px dashed var(--line2); border-radius:9px; padding:26px; text-align:center; font-size:12.5px; }
            .fade { animation:fade .25s ease both; } @keyframes fade { from{opacity:0; transform:translateY(4px);} to{opacity:1; transform:none;} }
            a { color:var(--info); }

            /* policy advisories (V7) + prompt-preview modal */
            .warn { color:var(--run); font-size:11px; line-height:1.55; margin:-4px 0 10px; border-left:2px solid var(--run);
              padding-left:8px; background:#15110611; }
            .modal { position:fixed; inset:0; background:rgba(2,4,8,.72); display:flex; align-items:center; justify-content:center; z-index:50; padding:30px; }
            .modal .box { background:linear-gradient(180deg,var(--panel),var(--panel2)); border:1px solid var(--line2); border-radius:11px;
              max-width:880px; width:100%; max-height:86vh; display:flex; flex-direction:column; box-shadow:0 18px 60px rgba(0,0,0,.6); }
            .modal .mhead { display:flex; align-items:center; justify-content:space-between; gap:12px; padding:13px 16px; border-bottom:1px solid var(--line); }
            .modal .mbody { padding:16px; overflow:auto; }
            .modal .x { background:transparent; border:1px solid var(--line2); color:var(--mut); border-radius:6px; cursor:pointer;
              padding:4px 11px; font-family:var(--mono); font-size:12px; }
            .modal .x:hover { color:var(--fg); border-color:var(--bad); }

            /* connection-lost banner (clear recovery message instead of a bare "failed to fetch") */
            #conn-banner { position:fixed; left:50%; bottom:18px; transform:translateX(-50%); z-index:60; display:none;
              align-items:center; gap:10px; background:#190a0c; color:var(--bad); border:1px solid #5a2027; border-radius:8px;
              padding:9px 14px; font-size:12px; box-shadow:0 10px 30px rgba(0,0,0,.5); max-width:90vw; }
            #conn-banner .dot { background:var(--bad); }

            /* progressive disclosure for the Create form's advanced options */
            details.adv { border:1px solid var(--line); border-radius:8px; margin:8px 0 4px; background:#0c0f16; }
            details.adv > summary { cursor:pointer; padding:10px 13px; color:var(--mut); font-size:12px; letter-spacing:.04em; list-style:none; user-select:none; }
            details.adv > summary::-webkit-details-marker { display:none; }
            details.adv > summary::before { content:"▸  "; color:var(--sig); }
            details.adv[open] > summary::before { content:"▾  "; }
            details.adv[open] > summary { color:var(--fg); border-bottom:1px solid var(--line); }
            details.adv .adv-body { padding:14px; }

            /* per-tab explainer banner — says what the tab is for and how to use it, in plain words */
            .intro { display:flex; gap:10px; align-items:flex-start; background:#0c111b; border:1px solid var(--line);
              border-left:3px solid var(--info); border-radius:8px; padding:11px 13px; margin-bottom:16px; font-size:12px; color:var(--mut); line-height:1.55; }
            .intro .i-ico { color:var(--info); font-size:13px; line-height:1.4; }
            .intro b { color:var(--fg); font-weight:600; }
            .intro code { color:var(--sig); background:#08160e; border:1px solid var(--sig-dim); border-radius:4px; padding:0 4px; font-size:11px; }

            /* guided Create form: labelled sections + an inline action-verb legend */
            .csec { margin:20px 0 10px; } .csec:first-of-type { margin-top:4px; }
            .csec-h { font-size:11px; letter-spacing:.1em; text-transform:uppercase; color:var(--sig); font-weight:700; }
            .csec-d { font-size:11.5px; color:var(--mut); margin-top:2px; }
            .csec-hr { height:1px; background:var(--line); margin:6px 0 14px; }
            .verbs { display:grid; grid-template-columns:repeat(auto-fill,minmax(220px,1fr)); gap:4px 16px; margin-top:7px;
              font-size:11px; color:var(--mut); }
            .verbs span b { color:var(--fg); font-weight:600; } .verbs .tgt { color:var(--viol); }
          </style>
        </head>
        <body>
          <div class="shell">
            <div class="topbar">
              <span class="brand">Agent<b>Loop</b> // Mission Control</span>
              <span class="clock" id="clock"></span>
              <div class="stat-strip">
                <span class="stat"><span class="dot run" id="d-run"></span>RUNNING <b id="s-run">0</b></span>
                <span class="stat"><span class="dot ok"></span>PASSED <b id="s-pass">0</b></span>
                <span class="stat"><span class="dot bad"></span>FAILED <b id="s-fail">0</b></span>
                <span class="env-tag" title="Local developer tool. Never run in CI; never exposed beyond loopback.">localhost · not for CI</span>
              </div>
            </div>
            <nav class="rail">
              <button data-tab="catalog" class="active" title="Browse the tests found under tests/*.yaml, grouped by suite. Click Launch to run one.">
                <span class="ico">▦</span> Catalog</button>
              <button data-tab="create" title="Author a new test as a guided form. It writes a validated YAML test under tests/created/ AND a Symphony ticket under tickets/created/ — you can also edit them by hand.">
                <span class="ico">+</span> Create</button>
              <button data-tab="tickets" title="Symphony tickets (tickets/*.md): a portable contract — view one, or Run it through the same scripts/run-ticket-proof.ps1 that CI uses.">
                <span class="ico">◆</span> Tickets</button>
              <button data-tab="runs" title="Recorded run history from runs/: result, score, steps, screenshots, and the OpenTelemetry trace link.">
                <span class="ico">≡</span> Runs</button>
              <button data-tab="live" title="Watch runs in progress: streaming logs, current step, and live screenshots. Launching a test spawns the CLI.">
                <span class="ico">◉</span> Live</button>
              <button data-tab="files" title="The on-disk files the dashboard reflects (tests/, runs/, config) — copy a path to edit it in your editor or CI.">
                <span class="ico">⌗</span> Files</button>
              <div class="spacer"></div>
              <div class="hint">Tests come from <b>tests/</b>, results from <b>runs/</b>. Launching spawns the CLI — no new data store.</div>
            </nav>
            <main>
              <section id="tab-catalog"></section>
              <section id="tab-create" class="hidden"></section>
              <section id="tab-tickets" class="hidden"></section>
              <section id="tab-runs" class="hidden"></section>
              <section id="tab-live" class="hidden"></section>
              <section id="tab-files" class="hidden"></section>
            </main>
          </div>
          <div id="conn-banner"><span class="dot"></span><span id="conn-msg">Lost connection to the dashboard server — it may have stopped. Restart it; this clears automatically.</span></div>

          <script>
            const $ = (s,r=document)=>r.querySelector(s);
            const el=(t,p={})=>Object.assign(document.createElement(t),p);
            const esc=s=>(s==null?"":String(s)).replace(/[&<>]/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;"}[c]));
            const escAttr=s=>esc(s).replace(/"/g,"&quot;").replace(/'/g,"&#39;");
            let CONFIG={traceUiTemplate:""};
            const rcls=r=>(r==="Passed"||r==="Succeeded")?"ok":((r==="Aborted"||r==="Failed"||r==="Blocked")?"bad":"run");
            const rtip=r=>({Passed:"The test reached its success condition.",Succeeded:"The run reached its success condition.",Failed:"Reached max steps without meeting the success condition.",Aborted:"Stopped early: score fell below the abort threshold or a quality guard aborted.",Blocked:"Could not start — the target window was not found.",Running:"In progress."}[r]||r);
            // Show/hide the connection-lost banner. A network-level fetch failure (server stopped,
            // port closed) surfaces as a clear, actionable message instead of a bare "failed to fetch".
            function setConn(ok){ const b=$("#conn-banner"); if(b) b.style.display=ok?"none":"flex"; }
            async function api(path,opts){
              let res;
              try{ res=await fetch(path,opts); }
              catch(_){ setConn(false); throw new Error("Cannot reach the dashboard server (localhost). Is it still running?"); }
              setConn(true);
              const ct=res.headers.get("content-type")||"";
              const data=ct.includes("json")?await res.json():await res.text();
              if(!res.ok) throw new Error((data&&data.error)||res.statusText); return data;
            }
            const dur=ms=>{ if(ms<0)ms=0; const s=Math.floor(ms/1000); const m=Math.floor(s/60);
              return m>0?`${m}m ${String(s%60).padStart(2,"0")}s`:`${s}.${String(Math.floor((ms%1000)/100))}s`; };

            // clock + aggregate status
            setInterval(()=>{ $("#clock").textContent=new Date().toTimeString().slice(0,8)+" UTC".replace("UTC",""); },1000);
            async function refreshStatus(){
              try{
                const [runs,jobs]=await Promise.all([api("/api/runs"),api("/api/jobs")]);
                const pass=runs.runs.filter(r=>r.result==="Passed"||r.result==="Succeeded").length;
                const fail=runs.runs.filter(r=>["Failed","Aborted","Blocked"].includes(r.result)).length;
                const run=jobs.jobs.filter(j=>j.status==="running").length;
                $("#s-pass").textContent=pass; $("#s-fail").textContent=fail; $("#s-run").textContent=run;
                $("#d-run").className="dot "+(run?"run":"");
              }catch{}
            }

            // nav
            document.querySelectorAll(".rail button[data-tab]").forEach(b=>b.onclick=()=>show(b.dataset.tab));
            let liveTimer=null,tickTimer=null;
            function show(tab){
              document.querySelectorAll(".rail button[data-tab]").forEach(b=>b.classList.toggle("active",b.dataset.tab===tab));
              ["catalog","create","tickets","runs","live","files"].forEach(t=>$("#tab-"+t).classList.toggle("hidden",t!==tab));
              if(location.hash.slice(1)!==tab) location.hash=tab;
              if(tab!=="live"){ clearInterval(liveTimer); clearInterval(tickTimer); liveTimer=tickTimer=null; }
              if(tab==="catalog")loadCatalog();
              if(tab==="create")loadCreate();
              if(tab==="tickets")loadTickets();
              if(tab==="runs")loadRuns();
              if(tab==="live")loadLive();
              if(tab==="files")loadFiles();
            }
            const headHTML=(title,sub)=>`<div class="head fade"><h2>${title}</h2><span class="sub">${sub}</span></div>`;
            const intro=(html)=>`<div class="intro fade"><span class="i-ico">ⓘ</span><div>${html}</div></div>`;

            // CATALOG
            let CATALOG=[], MAXC=2; const SEL=new Set();
            const FILT={q:"",framework:"",priority:"",category:"",suite:""};
            const tkey=t=>t.planPath+"|"+t.id;
            const distinct=k=>[...new Set(CATALOG.map(t=>k==="suite"?(t.suite||""):t[k]).filter(v=>v!=null&&v!==""))].map(String).sort();
            async function loadCatalog(){
              const host=$("#tab-catalog");
              host.innerHTML=headHTML("Catalog","Tests under tests/ — filter, then Launch one or batch-run a selection through the bounded queue")
                +intro("Your test backlog, read live from <code>tests/*.yaml</code>. Narrow it with the filters, then <b>▶ Launch</b> one test — or tick several and <b>Run selected</b>. Runs go through a bounded queue (<b>max parallel</b>) so the desktop isn't overwhelmed. On each card: <b>⌘ Prompt</b> previews the exact LLM prompt (no run), <b>✎ Edit</b> reopens dashboard-authored tests, <b>⇩ Archive</b> hides one (reversible, shows in Git). <b>⚠</b> notes are non-fatal policy advisories — the test still runs.")
                +"<div class='panel pad' id='cat-bar'></div><div id='cat'></div><div id='cat-arch'></div>";
              try{
                const [d,cfg]=await Promise.all([api("/api/tests"),api("/api/config").catch(()=>({maxConcurrency:2}))]);
                CATALOG=d.tests||[]; MAXC=cfg.maxConcurrency||2; SEL.clear();
                if(!CATALOG.length){ $("#cat").innerHTML="<div class='empty'>No tests found under tests/. Author one in <b>Create</b>.</div>"; }
                else { renderBar(); renderCatalog(); }
                loadArchived();
              }catch(e){ $("#cat").innerHTML=`<div class='empty'>${esc(e.message)}</div>`; }
            }
            async function loadArchived(){
              const host=$("#cat-arch"); if(!host) return;
              try{
                const a=await api("/api/archived");
                if(!a.count){ host.innerHTML=""; return; }
                const rows=a.tests.map(t=>`<div class="row" style="justify-content:space-between;padding:7px 0;border-top:1px solid var(--line)">
                    <div class="row" style="gap:8px;flex-wrap:wrap"><b style="font-size:12.5px">${esc(t.id)}</b>
                      <span class="dim" style="font-size:11.5px">${esc(t.title||"")}</span>
                      ${t.framework?`<span class="chip fw">${esc(t.framework)}</span>`:""}
                      <span class="dim" style="font-size:11px">${esc(t.planPath)}</span></div>
                    <button class="ghost" data-rp="${escAttr(t.planPath)}" style="padding:4px 10px" title="Move this YAML back to tests/ (un-archive)">↥ Restore</button>
                  </div>`).join("");
                host.innerHTML=`<div class="panel pad fade" style="margin-top:18px">
                    <div class="head" style="margin-bottom:4px"><h2 style="font-size:12px">Archived (${a.count})</h2>
                      <span class="sub">Under tests/archived/ — excluded from runs &amp; CI. Restore moves the YAML back.</span></div>
                    ${rows}</div>`;
                host.querySelectorAll("button[data-rp]").forEach(b=>b.onclick=()=>unarchive(b.dataset.rp));
              }catch(e){ host.innerHTML=""; }
            }
            async function unarchive(planPath){
              try{ await api("/api/tests/unarchive",{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify({planPath})}); loadCatalog(); }
              catch(e){ alert("Restore failed: "+e.message); }
            }
            function renderBar(){
              const selFor=(k,label)=>`<select id="flt-${k}" title="Filter by ${label}"><option value="">${label}: all</option>`+
                distinct(k).map(v=>`<option ${v===FILT[k]?"selected":""}>${esc(v)}</option>`).join("")+`</select>`;
              $("#cat-bar").innerHTML=`
                <div class="row" style="gap:8px;flex-wrap:wrap;align-items:center">
                  <input id="flt-q" placeholder="search id / title / goal / tag" value="${escAttr(FILT.q)}"/>
                  ${selFor("category","category")} ${selFor("framework","framework")} ${selFor("priority","priority")} ${selFor("suite","suite")}
                  <button class="ghost" id="flt-clear" title="Clear all filters">clear</button>
                </div>
                <div class="row" style="gap:10px;margin-top:10px;align-items:center;flex-wrap:wrap">
                  <button class="act" id="run-sel" title="Queue the selected tests (run MAX at a time, the rest wait)">▶ Run selected (<span id="sel-n">0</span>)</button>
                  <button class="ghost" id="run-flt" title="Queue every test matching the current filter">▶ Run filtered</button>
                  <span class="dim" style="margin-left:auto">max parallel</span>
                  <input id="cc" type="number" min="1" max="16" value="${MAXC}" style="width:58px" title="Max runs executing at once; the rest queue. Guards against UIA/desktop contention."/>
                  <span class="dim" id="cat-count"></span>
                </div>`;
              $("#flt-q").oninput=e=>{FILT.q=e.target.value;renderCatalog();};
              ["category","framework","priority","suite"].forEach(k=>$("#flt-"+k).onchange=e=>{FILT[k]=e.target.value;renderCatalog();});
              $("#flt-clear").onclick=()=>{Object.keys(FILT).forEach(k=>FILT[k]="");SEL.clear();renderBar();renderCatalog();};
              $("#run-sel").onclick=()=>runMany([...SEL].map(k=>CATALOG.find(t=>tkey(t)===k)).filter(Boolean),"selected");
              $("#run-flt").onclick=()=>runMany(filtered(),"filtered");
              $("#cc").onchange=async e=>{ const v=Math.max(1,Math.min(16,parseInt(e.target.value)||2));
                try{ const r=await api("/api/jobs/concurrency",{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify({max:v})}); MAXC=r.maxConcurrency; }catch(_){ } e.target.value=MAXC; };
              updateSelN();
            }
            function filtered(){
              const q=FILT.q.toLowerCase();
              return CATALOG.filter(t=>
                (!FILT.category||String(t.category)===FILT.category)&&
                (!FILT.framework||t.framework===FILT.framework)&&
                (!FILT.priority||t.priority===FILT.priority)&&
                (!FILT.suite||(t.suite||"")===FILT.suite)&&
                (!q||[t.id,t.title,t.goal,(t.tags||[]).join(" ")].join(" ").toLowerCase().includes(q)));
            }
            function renderCatalog(){
              const list=filtered(), cat=$("#cat"); const cc=$("#cat-count"); if(cc) cc.textContent=list.length+" / "+CATALOG.length+" shown";
              if(!list.length){ cat.innerHTML="<div class='empty'>No tests match the filter.</div>"; return; }
              const by={}; list.forEach(t=>(by[t.suite||"(no suite)"]||=[]).push(t));
              cat.innerHTML="";
              Object.keys(by).sort().forEach(s=>{
                const sec=el("div",{className:"fade",style:"margin-bottom:20px"});
                sec.innerHTML=`<div class="row" style="margin-bottom:9px"><span class="chip">${esc(s)}</span><span class="dim">${by[s].length} test${by[s].length>1?"s":""}</span></div>`;
                const g=el("div",{className:"grid"}); by[s].forEach(t=>g.appendChild(card(t))); sec.appendChild(g); cat.appendChild(sec);
              });
            }
            const catColor={Smoke:"#46e08a",Monkey:"#e0b446",Scenario:"#5aa9e0",Regression:"#c98ae0",Exploratory:"#e0795a"};
            function card(t){
              const c=el("div",{className:"card"}), k=tkey(t), cc=catColor[t.category]||"#7a8699";
              c.innerHTML=`<div class="row" style="justify-content:space-between;align-items:flex-start">
                  <h4 style="margin:0">${esc(t.id)}</h4>
                  <label class="dim" style="font-size:11px;cursor:pointer" title="Select for a batch run"><input type="checkbox" class="csel" ${SEL.has(k)?"checked":""}/> sel</label></div>
                <div class="mut" style="font-size:12px;margin:3px 0 9px">${esc(t.title||"—")}</div>
                <div class="row" style="margin-bottom:10px;flex-wrap:wrap">
                  <span class="chip" style="border-color:${cc};color:${cc}" title="Test category (drives the agent's prompt persona)">${esc(t.category||"Scenario")}</span>
                  ${t.framework?`<span class="chip fw">${esc(t.framework)}</span>`:""}
                  ${t.priority?`<span class="chip">${esc(t.priority)}</span>`:""}
                  ${(t.tags||[]).slice(0,4).map(x=>`<span class="chip">${esc(x)}</span>`).join("")}
                </div>
                <div class="dim" style="font-size:11.5px;margin-bottom:11px;min-height:32px">${esc(t.goal||"")}</div>
                ${(t.warnings&&t.warnings.length)?`<div class="warn" title="Non-fatal policy advisories (same as --validate-plan). The test still runs.">${t.warnings.map(w=>"⚠ "+esc(w)).join("<br>")}</div>`:""}`;
              c.querySelector(".csel").onchange=e=>{ e.target.checked?SEL.add(k):SEL.delete(k); updateSelN(); };
              const bar=el("div",{className:"row",style:"gap:6px;flex-wrap:wrap"});
              const lb=el("button",{className:"act",textContent:"▶ Launch",title:"Queue this test (bounded by max parallel). Opens Live."}); lb.onclick=()=>runMany([t],"one"); bar.appendChild(lb);
              const pb=el("button",{className:"ghost",textContent:"⌘ Prompt",title:"Preview the exact prompt the LLM would receive (key-free, no run) — same as --show-prompt."}); pb.onclick=()=>showPrompt(t); bar.appendChild(pb);
              if(t.editable){ const eb=el("button",{className:"ghost",textContent:"✎ Edit",title:"Edit this dashboard-authored YAML — reopens the form; saving overwrites it, re-validated."}); eb.onclick=()=>editTest(t); bar.appendChild(eb); }
              if(t.archivable){ const ab=el("button",{className:"ghost",textContent:"⇩ Archive",title:"Move this YAML to tests/archived/ (excluded from catalog + CI; reversible, shows in Git). Not a hard delete."}); ab.onclick=()=>archiveTest(t); bar.appendChild(ab); }
              c.appendChild(bar);
              return c;
            }
            function updateSelN(){ const n=$("#sel-n"); if(n)n.textContent=SEL.size; }
            async function runMany(tests,label){
              if(!tests.length){ alert("Nothing to run."); return; }
              if(tests.length>5 && !confirm(`Queue ${tests.length} runs (${label})? They execute ${MAXC} at a time.`)) return;
              for(const t of tests){
                try{ await api("/api/runs",{method:"POST",headers:{"content-type":"application/json"},
                  body:JSON.stringify({planPath:t.planPath,testId:t.id,window:t.targetWindow})}); }
                catch(e){ alert("Launch failed for "+t.id+": "+e.message); }
              }
              show("live");
            }
            async function archiveTest(t){
              if(!confirm(`Archive ${t.id}? Moves ${t.planPath} to tests/archived/ (reversible, shows in Git).`)) return;
              try{ await api("/api/tests/archive",{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify({planPath:t.planPath})}); loadCatalog(); }
              catch(e){ alert("Archive failed: "+e.message); }
            }
            function editTest(t){ show("create"); fillCreate(t); }
            function fillCreate(t){
              const set=(k,v)=>{ const f=$("#f-"+k); if(f!=null&&v!=null) f.value=v; };
              set("id",t.id); set("suite",t.suite); set("title",t.title); set("framework",t.framework); set("priority",t.priority);
              set("category",t.category); set("risk",t.risk);
              set("targetWindow",t.targetWindow); set("goal",t.goal); set("successCondition",t.successCondition);
              set("maxSteps",t.maxSteps); set("actions",(t.allowedActions||[]).join(", ")); set("tags",(t.tags||[]).join(", "));
              const h=$("#f-hint"); if(h) h.textContent="Editing "+t.id+" — saving overwrites its YAML (re-validated).";
            }
            // V7: preview the exact prompt the LLM would receive (key-free; reuses PromptBuilder).
            async function showPrompt(t){
              const m=el("div",{className:"modal fade"});
              m.innerHTML=`<div class="box"><div class="mhead">
                  <div class="row" style="gap:8px"><span class="chip info">prompt</span><b>${esc(t.id)}</b><span class="dim" style="font-size:11px">${esc(t.planPath)}</span></div>
                  <button class="x" title="Close (Esc)">✕ close</button></div>
                <div class="mbody"><div class="dim" style="font-size:11px;margin-bottom:8px">Exact prompt the LLM would receive — key-free preview, secrets redacted, the live UI snapshot is injected at runtime. Same as <b>--show-prompt</b>.</div>
                <pre class="term" style="max-height:none"><span class="dim">Loading…</span></pre></div></div>`;
              document.body.appendChild(m);
              const pre=m.querySelector("pre");
              const close=()=>{ m.remove(); document.removeEventListener("keydown",onKey); };
              const onKey=e=>{ if(e.key==="Escape") close(); };
              m.querySelector(".x").onclick=close;
              m.onclick=e=>{ if(e.target===m) close(); };
              document.addEventListener("keydown",onKey);
              try{ const d=await api(`/api/prompt?planPath=${encodeURIComponent(t.planPath)}&testId=${encodeURIComponent(t.id)}`);
                pre.textContent=d.prompt; }
              catch(e){ pre.innerHTML=`<span style="color:var(--bad)">${esc(e.message)}</span>`; }
            }

            // CREATE
            // section header + a text/textarea/select field builder, each with a plain-language explainer.
            const csec=(t,d)=>`<div class="csec"><div class="csec-h">${t}</div><div class="csec-d">${d}</div></div><div class="csec-hr"></div>`;
            const fField=(k,label,req,help,ph,ta)=>`<div class="field"><label>${label} ${req?'<span class="req">*</span>':''}</label>
                <span class="help">${help}</span>${ta?`<textarea id="f-${k}" rows="2" placeholder="${escAttr(ph)}"></textarea>`:`<input id="f-${k}" placeholder="${escAttr(ph)}"/>`}</div>`;
            const fSelect=(k,label,help,opts,val)=>`<div class="field"><label>${label}</label><span class="help">${help}</span>
                <select id="f-${k}">${opts.map(o=>{const[v,t]=Array.isArray(o)?o:[o,o];return `<option value="${escAttr(v)}"${v===val?" selected":""}>${esc(t)}</option>`;}).join("")}</select></div>`;
            function loadCreate(){
              const verbLegend=`<div class="verbs">
                  <span><b>EnterText</b> <span class="tgt">⌖</span> — type into a field</span>
                  <span><b>Click</b> <span class="tgt">⌖</span> — click a control</span>
                  <span><b>DoubleClick</b> <span class="tgt">⌖</span> — double-click a control</span>
                  <span><b>Scroll</b> <span class="tgt">⌖</span> — scroll (value = up/down)</span>
                  <span><b>Assert</b> <span class="tgt">⌖</span> — verify a control's text/state</span>
                  <span><b>Wait</b> — pause for the UI to settle</span>
                  <span><b>Explore</b> — inspect the UI to find controls</span>
                  <span><b>Done</b> — declare the goal achieved</span>
                </div><div class="dim" style="font-size:10.5px;margin-top:5px"><span class="tgt">⌖</span> = needs a target control. Restrict the list to keep a test focused; leave the default for general flows.</div>`;
              $("#tab-create").innerHTML=headHTML("Create a test","A guided form — every field is explained. Fill it, Validate &amp; save, and you get a ready-to-run YAML test (editable by hand anytime).")+
                intro("This writes a validated <b>YAML test</b> under <code>tests/created/</code> plus a runnable <b>ticket</b> — the exact same files the CLI and CI use. Nothing here is dashboard-only: you can open and edit them in your editor. Fields marked <span class='req'>*</span> are required; everything else has a sensible default.")+
                `<div class="panel pad form fade">

                  ${csec("1 · Identity &amp; triage","How this test is named, grouped, and prioritised in the catalog.")}
                  ${fField("id","Test ID","required","A unique identifier. Used in the catalog, in run artifacts, and to select the test from the CLI (<code>--test-id</code>). Convention: UPPER-CASE-WITH-DASHES.","LOGIN-HAPPY-001")}
                  <div class="two">
                    ${fField("title","Title","","A short human-readable name shown in the catalog and reports.","Login — happy path")}
                    ${fField("suite","Suite","","Logical group this test belongs to (a bucket in the catalog). Defaults to <code>created</code>.","login")}
                  </div>
                  <div class="two">
                    ${fSelect("category","Category","Shapes the agent's persona &amp; prompt. <b>Scenario</b> = directed business flow with a clear goal (default). <b>Smoke</b> = quick \"does it open / basic path works\". <b>Monkey</b> = stress/explore to surface crashes. <b>Audit</b> = inspect UI/accessibility only, no changes.",[["Scenario","Scenario — directed flow (default)"],["Smoke","Smoke — quick basic-path check"],["Monkey","Monkey — stress / explore"],["Audit","Audit — inspect only, no changes"]],"Scenario")}
                    ${fSelect("priority","Priority","Triage importance for humans &amp; CI ordering. <b>P1</b> = must-pass / critical · <b>P2</b> = important · <b>P3</b> = nice-to-have. Optional.",[["","— none —"],"P1","P2","P3"],"")}
                  </div>
                  ${fSelect("risk","Risk","How damaging a wrong action would be. Higher risk tightens the runner's guards (e.g. protected actions may need confirmation). Leave blank if unsure.",[["","— none —"],["low","low"],["medium","medium"],["high","high"],["critical","critical"]],"")}

                  ${csec("2 · Target application","Which desktop app the agent drives, and how it finds it.")}
                  ${fSelect("framework","Framework","The desktop UI toolkit your target app is built with — it drives how the agent attaches and reads controls. <b>winforms</b> &amp; <b>wpf</b> = classic .NET desktop · <b>maui</b> = .NET MAUI (Windows) · <b>avalonia</b> = Avalonia desktop.",[["","— select —"],"winforms","wpf","maui","avalonia"],"")}
                  ${fField("targetWindow","Target window","","The exact title-bar text of the window to drive — the agent locates the app by this title. Tip: copy it from the running app's title bar.","Sample Login App (.NET 8)")}

                  ${csec("3 · What the agent should do","The task itself, how success is proven, and the actions allowed.")}
                  ${fField("goal","Goal","required","Plain-language description of the task — this is the heart of the test. Be specific: which fields, which values, which button, and what proves success.","Type 'admin' / 'password123', click Login, and confirm the welcome screen appears.",true)}
                  ${fField("successCondition","Success condition","","Text the app shows when the goal is met — the agent watches for it to mark the run <b>Passed</b>. Leave blank to instead prove success with an explicit <b>Assert</b> action.","Login successful")}
                  <div class="field"><label>Allowed actions</label><span class="help">The UI verbs the agent may use (comma-separated):</span>
                    <input id="f-actions" value="EnterText, Click, Assert, Done, Wait"/>${verbLegend}</div>
                  <div class="field"><label>Max steps</label><span class="help">Hard cap on agent iterations before the run fails. Keep it tight (5–15) for focused flows; higher allows more exploration but is slower/costlier. Above 100 raises a warning.</span><input id="f-maxSteps" type="number" min="1" value="8"/></div>

                  ${csec("4 · Evidence &amp; demo run","What to capture, and an optional helper for the built-in samples.")}
                  <div class="two">
                    ${fSelect("evidence","Evidence level","How much to capture per run. <b>minimal</b> = report + summary only (fastest) · <b>standard</b> = + screenshots (default, good for review) · <b>full</b> = + full UI-tree dumps (best for debugging selector issues).",[["standard","standard — + screenshots (default)"],["minimal","minimal — report only (fastest)"],["full","full — + UI-tree dumps (debug)"]],"standard")}
                    ${fSelect("launch","Launch sample","Only for the demo apps shipped with this repo: <b>yes</b> starts the sample app automatically around the run. For your own app, leave <b>no</b> and launch it yourself.",[["false","no — I'll launch the app myself"],["true","yes — start the built-in sample"]],"false")}
                  </div>
                  ${fField("tags","Tags","","Free comma-separated labels you can filter the catalog by. Optional.","smoke, login")}

                  <div class="row" style="margin-top:16px"><button class="act" id="f-save">✓ Validate &amp; save</button><span class="dim" id="f-hint">Validated with the same checker the CLI uses; also emits a runnable ticket.</span></div>
                  <div id="f-out" style="margin-top:14px"></div>
                </div>`;
              $("#f-save").onclick=saveTest;
            }
            async function saveTest(){
              const v=k=>($("#f-"+k)?.value||"").trim();
              const list=s=>s.split(",").map(x=>x.trim()).filter(Boolean);
              const req={id:v("id"),suite:v("suite"),title:v("title"),framework:v("framework"),priority:v("priority"),
                category:v("category")||null,risk:v("risk")||null,
                targetWindow:v("targetWindow"),goal:v("goal"),successCondition:v("successCondition")||null,
                maxSteps:parseInt(v("maxSteps"))||8,allowedActions:list(v("actions")),tags:list(v("tags")),
                evidenceLevel:v("evidence")||"standard",launchSample:v("launch")==="true"};
              const out=$("#f-out");
              try{ const r=await api("/api/tests",{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify(req)});
                const warns=(r.warnings&&r.warnings.length)?`<div class="warn" style="margin-bottom:10px" title="Non-fatal policy advisories — the test was still saved.">${r.warnings.map(w=>"⚠ "+esc(w)).join("<br>")}</div>`:"";
                out.innerHTML=`<div class="row" style="margin-bottom:8px"><span class="chip ok">test ${esc(r.planPath)}</span><span class="chip ok">ticket ${esc(r.ticketPath)}</span></div>${warns}<pre class="term">${esc(r.yaml)}</pre>`;
              }catch(e){ out.innerHTML=`<div class="chip bad">${esc(e.message)}</div>`; }
            }

            // RUNS
            async function loadRuns(){
              $("#tab-runs").innerHTML=headHTML("Runs","Recorded run history from runs/")+
                intro("Recorded history from <code>runs/</code>. Each row is one past run: its <b>result</b> (hover the chip for what it means), final <b>score</b>, number of steps, and start time. Click a run to expand its step-by-step actions, screenshots, and the OpenTelemetry <b>trace</b> link.")+
                "<div class='panel pad' id='runs-body'></div><div id='run-detail' style='margin-top:14px'></div>";
              try{
                const d=await api("/api/runs");
                if(!d.count){ $("#runs-body").innerHTML="<div class='empty'>No runs yet. Launch one from the Catalog.</div>"; return; }
                const rows=d.runs.map(r=>`<tr class="clk" data-id="${esc(r.runId)}" title="Click to expand steps, screenshots and trace">
                  <td><span class="chip ${rcls(r.result)}" title="${escAttr(rtip(r.result))}">${esc(r.result)}</span></td>
                  <td>${esc(r.testId||r.runId)}</td><td class="fw">${esc(r.framework||"")}</td>
                  <td style="font-variant-numeric:tabular-nums">${r.finalScore}</td><td>${r.steps}</td>
                  <td class="mut">${esc((r.startedAt||"").replace("T"," ").slice(0,19))}</td></tr>`).join("");
                $("#runs-body").innerHTML=`<table><thead><tr><th>Result</th><th>Test</th><th>Framework</th><th>Score</th><th>Steps</th><th>Started</th></tr></thead><tbody>${rows}</tbody></table>`;
                $("#runs-body").querySelectorAll("tr.clk").forEach(tr=>tr.onclick=()=>showRun(tr.dataset.id));
              }catch(e){ $("#runs-body").innerHTML=`<div class='empty'>${esc(e.message)}</div>`; }
            }
            async function showRun(id){
              const host=$("#run-detail"); host.innerHTML="<div class='panel pad mut'>Loading…</div>";
              try{
                const r=await api("/api/runs/"+id);
                const trace=r.traceId?(CONFIG.traceUiTemplate
                  ? `<a href="${escAttr(CONFIG.traceUiTemplate.replace("{traceId}",r.traceId))}" target="_blank">${esc(r.traceId)} ↗</a>`
                  : `<span class="chip info">trace ${esc(r.traceId)}</span> <span class="dim">open in Aspire → Traces</span>`):"<span class='dim'>no trace</span>";
                const steps=(r.steps||[]).map(s=>`<tr><td>${s.stepNumber}</td><td>${esc(s.actionType)}</td><td class="mut">${esc(s.actionTarget||"")}</td>
                  <td><span class="chip ${s.outcome==="Succeeded"?"ok":"bad"}">${esc(s.outcome)}</span></td>
                  <td class="mut">${esc(s.failureCode||s.guardCode||"")}</td><td style="font-variant-numeric:tabular-nums">${s.cumulativeScore}</td></tr>`).join("");
                host.innerHTML=`<div class="panel pad fade">
                  <div class="row" style="margin-bottom:8px"><h2 style="margin:0;font-size:14px;letter-spacing:.05em">RUN ${esc(r.runId)}</h2>
                    <span class="chip ${rcls(r.result)}" title="${escAttr(rtip(r.result))}">${esc(r.result)}</span><span class="dim">${esc(r.goalDescription||"")}</span></div>
                  <div class="row" style="margin-bottom:12px">${trace}</div>
                  <table><thead><tr><th>#</th><th>Action</th><th>Target</th><th>Outcome</th><th>Failure/Guard</th><th>Score</th></tr></thead><tbody>${steps}</tbody></table>
                  <div class="head" style="margin:16px 0 8px"><h2 style="font-size:12px">Screenshots</h2></div>
                  <div class="shots grid2" id="run-shots"><span class="dim">…</span></div></div>`;
                loadShots(id,$("#run-shots"));
              }catch(e){ host.innerHTML=`<div class='empty'>${esc(e.message)}</div>`; }
            }
            async function loadShots(id,target){
              try{ const d=await api("/api/runs/"+id+"/screenshots");
                if(!d.screenshots.length){ target.innerHTML="<span class='dim'>No screenshots (evidence level may be minimal).</span>"; return; }
                target.innerHTML=""; d.screenshots.forEach(f=>target.appendChild(el("img",{src:`/api/screenshot?run=${encodeURIComponent(id)}&file=${encodeURIComponent(f)}`,loading:"lazy",title:f})));
              }catch(e){ target.innerHTML=`<span class='dim'>${esc(e.message)}</span>`; }
            }

            // FILES — on-disk source-of-truth explorer
            const fsize=n=>n<1024?n+" B":(n<1048576?(n/1024).toFixed(1)+" KB":(n/1048576).toFixed(1)+" MB");
            async function loadFiles(){
              $("#tab-files").innerHTML=headHTML("Files","On-disk sources the dashboard reflects — edit them in your editor or CI; no UI required")+
                intro("A <b>read-only</b> mirror of the on-disk sources: <code>tests/</code> (your YAML — the source of truth), <code>runs/</code> (artifacts), plus key config. Click a text file to preview it; use <b>copy</b> to grab a path and edit it in your own editor. The dashboard never hides where the real files live — it's a view over them, not a replacement.")+
                `<div class="row" style="margin-bottom:12px"><span class="chip">tests/ = YAML source of truth</span><span class="chip">runs/ = artifacts</span><span class="dim" style="font-size:11.5px">click a text file to preview · copy a path to open it manually</span></div>
                 <div style="display:grid;grid-template-columns:minmax(320px,460px) 1fr;gap:14px"><div class="panel pad fade" id="ftree"></div><div class="panel pad" id="fview"><span class="dim">Select a file to preview.</span></div></div>`;
              try{
                const d=await api("/api/files");
                if(!d.count){ $("#ftree").innerHTML="<div class='empty'>No files yet.</div>"; return; }
                const root={};
                d.files.forEach(f=>{ const parts=f.path.split("/"); let node=root;
                  parts.forEach((p,i)=>{ node.children=node.children||{};
                    node.children[p]=node.children[p]||{name:p}; if(i===parts.length-1) node.children[p].file=f; node=node.children[p]; }); });
                $("#ftree").innerHTML=""; $("#ftree").appendChild(treeNode(root,true));
                if(d.capped) $("#ftree").appendChild(el("div",{className:"dim",style:"margin-top:8px;font-size:11px",textContent:"(list capped at 2000 entries)"}));
              }catch(e){ $("#ftree").innerHTML=`<div class='empty'>${esc(e.message)}</div>`; }
            }
            function treeNode(node,top){
              const wrap=el("div");
              const kids=node.children?Object.values(node.children).sort((a,b)=>((a.file?1:0)-(b.file?1:0))||a.name.localeCompare(b.name)):[];
              kids.forEach(k=>{
                if(k.file){
                  const row=el("div",{className:"row",style:"justify-content:space-between;padding:2px 0;gap:8px"});
                  const nm=el("span",{textContent:"› "+k.name,style:"white-space:nowrap;overflow:hidden;text-overflow:ellipsis;flex:1;min-width:0"});
                  if(k.file.editable){ nm.style.cursor="pointer"; nm.style.color="var(--info)"; nm.onclick=()=>viewFile(k.file.path); }
                  const right=el("div",{className:"row",style:"gap:8px;flex:0 0 auto"});
                  right.innerHTML=`<span class="dim" style="font-size:10.5px;font-variant-numeric:tabular-nums">${fsize(k.file.size)}</span>`;
                  const cp=el("button",{className:"ghost",textContent:"copy",title:k.file.path,style:"padding:1px 7px;font-size:10.5px"});
                  cp.onclick=()=>{ try{ navigator.clipboard.writeText(k.file.path); cp.textContent="✓"; setTimeout(()=>cp.textContent="copy",900);}catch{} };
                  right.appendChild(cp); row.appendChild(nm); row.appendChild(right); wrap.appendChild(row);
                } else {
                  const det=el("details"); det.open=top;
                  det.appendChild(el("summary",{textContent:k.name+"/",style:"cursor:pointer;color:var(--fg);padding:3px 0;letter-spacing:.02em"}));
                  const inner=treeNode(k,false); inner.style.cssText="margin-left:12px;border-left:1px solid var(--line);padding-left:9px"; det.appendChild(inner); wrap.appendChild(det);
                }
              });
              return wrap;
            }
            async function viewFile(path){
              const v=$("#fview"); v.innerHTML="<span class='dim'>Loading…</span>";
              try{ const txt=await api("/api/file?path="+encodeURIComponent(path));
                v.innerHTML=`<div class="row" style="margin-bottom:8px;justify-content:space-between"><span class="chip info">${esc(path)}</span><span class="dim" style="font-size:11px">read-only — edit on disk</span></div><pre class="term" style="max-height:560px">${esc(txt)}</pre>`;
              }catch(e){ v.innerHTML=`<div class='empty'>${esc(e.message)}</div>`; }
            }

            // TICKETS — Symphony contract (same files CI runs via run-ticket-proof.ps1)
            async function loadTickets(){
              $("#tab-tickets").innerHTML=headHTML("Tickets","Symphony tickets under tickets/ — view one, or Run it through the same adapter CI uses")+
                intro("A <b>ticket</b> is a portable run contract (<code>tickets/*.md</code>) — the very same file CI executes via <code>scripts/run-ticket-proof.ps1</code>. Click a row to read it, or <b>▶ Run</b> to launch it through that exact adapter, so a run here is identical to a run in CI. Tickets are authored by <b>Create</b> or by hand.")+
                "<div class='panel pad' id='tk-body'></div><div id='tk-detail' style='margin-top:14px'></div>";
              try{
                const d=await api("/api/tickets");
                if(!d.count){ $("#tk-body").innerHTML="<div class='empty'>No tickets under tickets/. Use Create to author one.</div>"; return; }
                const rows=d.tickets.map(t=>`<tr class="clk" data-path="${escAttr(t.path)}">
                  <td>${esc(t.ticketId||t.path)}</td><td>${esc(t.title||"")}</td>
                  <td class="fw">${esc(t.framework||"")}</td><td class="mut">${esc(t.testId||"")}</td>
                  <td class="mut" style="font-size:11.5px">${esc(t.plan||"")}</td>
                  <td><button class="act" data-run="${escAttr(t.path)}" style="padding:4px 10px">▶ Run</button></td></tr>`).join("");
                $("#tk-body").innerHTML=`<table><thead><tr><th>Ticket</th><th>Title</th><th>Framework</th><th>Test</th><th>Plan</th><th></th></tr></thead><tbody>${rows}</tbody></table>`;
                $("#tk-body").querySelectorAll("tr.clk").forEach(tr=>tr.onclick=e=>{ if(e.target.dataset.run) return; viewTicket(tr.dataset.path); });
                $("#tk-body").querySelectorAll("button[data-run]").forEach(b=>b.onclick=()=>runTicket(b.dataset.run));
              }catch(e){ $("#tk-body").innerHTML=`<div class='empty'>${esc(e.message)}</div>`; }
            }
            async function viewTicket(path){
              const host=$("#tk-detail"); host.innerHTML="<div class='panel pad mut'>Loading…</div>";
              try{ const md=await api("/api/ticket?path="+encodeURIComponent(path));
                host.innerHTML=`<div class="panel pad fade"><div class="row" style="margin-bottom:8px;justify-content:space-between"><span class="chip info">${esc(path)}</span><button class="ghost" id="tk-run2">▶ Run this ticket</button></div><pre class="term" style="max-height:520px">${esc(md)}</pre></div>`;
                $("#tk-run2").onclick=()=>runTicket(path);
              }catch(e){ host.innerHTML=`<div class='empty'>${esc(e.message)}</div>`; }
            }
            async function runTicket(path){
              try{ await api("/api/tickets/run",{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify({ticketPath:path})}); show("live"); }
              catch(e){ alert("Run ticket failed: "+e.message); }
            }

            // LIVE — supervision console
            const endStamp={};            // jobId -> client end time (ms) when first seen exited
            function logLine(line){
              const m=line.match(/\]\s\[([A-Z]+)\]/);
              const lvl=m?m[1]:"";
              const body=esc(line);
              return `<div class="ln l-${lvl}"><span class="lvl">${lvl||"·"}</span>${body.replace(/^\S+\s\[[A-Z]+\]\s?/, "")}</div>`;
            }
            function stepOf(logs){ let cur=0,tot=0; for(const l of logs){ const m=l.match(/\[Step (\d+)\/(\d+)\]/); if(m){cur=+m[1];tot=+m[2];} } return {cur,tot}; }
            async function loadLive(){
              clearInterval(liveTimer); clearInterval(tickTimer);
              $("#tab-live").innerHTML=headHTML("Live telemetry",'<span class="dot run" style="display:inline-block;margin-right:6px"></span>polling every 2s · launched runs spawn the CLI (need your target app + .env)')+
                intro("Watch runs in progress. Launching a test spawns the CLI — it needs your <b>target app running</b> and a <code>.env</code> with the LLM provider key. Each channel streams its <b>step progress</b>, log lines, and <b>live screenshots</b>; finished channels show the exit code. Queued runs wait for a free slot (set by <b>max parallel</b> in the Catalog).")+
                "<div id='live'></div>";
              const render=async()=>{
                try{
                  const d=await api("/api/jobs");
                  const host=$("#live");
                  if(!d.jobs.length){ host.innerHTML="<div class='empty'>No active channels. Launch a run from the Catalog to start streaming.</div>"; return; }
                  // diff-render: build once, then only update volatile bits to keep autoscroll/screenshots stable
                  d.jobs.forEach(j=>{
                    if(j.status!=="running" && !endStamp[j.jobId]) endStamp[j.jobId]=Date.now();
                    let cardEl=$("#job-"+j.jobId);
                    if(!cardEl){ cardEl=el("div",{id:"job-"+j.jobId,className:"panel pad fade",style:"margin-bottom:12px"}); host.appendChild(cardEl); cardEl.dataset.k=""; }
                    const st=stepOf(j.logs||[]); const isQ=j.status==="queued";
                    const cls=isQ?"info":(j.status==="running"?"running":(j.exitCode===0?"ok":"bad"));
                    cardEl.className="panel pad live-card "+cls;
                    const key=JSON.stringify([j.status,j.exitCode,j.runId,(j.logs||[]).length,st.cur,st.tot]);
                    if(cardEl.dataset.k!==key){
                      cardEl.dataset.k=key;
                      const dotc=isQ?"info":(j.status==="running"?"run":(j.exitCode===0?"ok":"bad"));
                      cardEl.innerHTML=`
                        <div class="row" style="justify-content:space-between">
                          <div class="row">
                            <span class="dot ${dotc}"></span><b style="letter-spacing:.03em">${esc(j.testId)}</b>
                            <span class="chip ${cls==="running"?"run":cls}">${esc(j.status)}${j.exitCode!=null?(" · exit "+j.exitCode):""}</span>
                          </div>
                          <div class="row">
                            ${j.runId?`<span class="chip info" title="The run id; its artifacts are under runs/${esc(j.runId)}/">run ${esc(j.runId)}</span>`:'<span class="chip" title="Recovered from the runner logs once the run starts">awaiting run id…</span>'}
                            ${j.pid?`<span class="chip" title="Process id of the spawned AgentRunner CLI">pid ${j.pid}</span>`:'<span class="chip" title="Waiting for a free concurrency slot">queued…</span>'}
                            <span class="chip" title="Elapsed time"><span class="timer" data-start="${new Date(j.startedAt).getTime()}" data-job="${esc(j.jobId)}"></span></span>
                          </div>
                        </div>
                        ${st.tot?`<div class="row" style="margin:11px 0 4px"><span class="dim" style="font-size:10.5px;letter-spacing:.08em">STEP ${st.cur} / ${st.tot}</span></div>
                          <div class="progress"><i style="width:${Math.round(st.cur/st.tot*100)}%"></i></div>`:""}
                        <div class="head" style="margin:13px 0 6px"><h2 style="font-size:11px">Log stream</h2></div>
                        <pre class="term" id="log-${esc(j.jobId)}">${(j.logs||[]).slice(-200).map(logLine).join("")||'<span class="dim">(starting…)</span>'}</pre>
                        <div class="head" style="margin:13px 0 6px"><h2 style="font-size:11px">Live screenshots</h2></div>
                        <div class="shots" id="shots-${esc(j.jobId)}"><span class="dim">${j.runId?"…":"waiting for run id"}</span></div>`;
                      const log=$("#log-"+j.jobId); if(log) log.scrollTop=log.scrollHeight;
                      if(j.runId) loadShots(j.runId,$("#shots-"+j.jobId));
                    }
                  });
                  // drop cards for jobs no longer present
                  host.querySelectorAll("[id^='job-']").forEach(c=>{ if(!d.jobs.find(j=>"job-"+j.jobId===c.id)) c.remove(); });
                }catch(e){ $("#live").innerHTML=`<div class='empty'>${esc(e.message)}</div>`; }
              };
              await render();
              liveTimer=setInterval(render,2000);
              tickTimer=setInterval(()=>{ document.querySelectorAll(".timer").forEach(t=>{
                const start=+t.dataset.start; const end=endStamp[t.dataset.job]||Date.now(); t.textContent=dur(end-start); }); },250);
            }

            const validTab=t=>["catalog","create","tickets","runs","live","files"].includes(t);
            window.addEventListener("hashchange",()=>{ const t=location.hash.slice(1); if(validTab(t)) show(t); });
            (async()=>{ try{CONFIG=await api("/api/config");}catch{} refreshStatus(); setInterval(refreshStatus,5000);
              show(validTab(location.hash.slice(1))?location.hash.slice(1):"catalog"); })();
          </script>
          <style>.hidden{display:none!important;}</style>
        </body>
        </html>
        """;
}
