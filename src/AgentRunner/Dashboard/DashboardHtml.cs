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
              <button data-tab="create" title="Author a new test as a guided form. It writes a validated YAML file under tests/created/ — you can also edit it by hand.">
                <span class="ico">+</span> Create</button>
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
              <section id="tab-runs" class="hidden"></section>
              <section id="tab-live" class="hidden"></section>
              <section id="tab-files" class="hidden"></section>
            </main>
          </div>

          <script>
            const $ = (s,r=document)=>r.querySelector(s);
            const el=(t,p={})=>Object.assign(document.createElement(t),p);
            const esc=s=>(s==null?"":String(s)).replace(/[&<>]/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;"}[c]));
            const escAttr=s=>esc(s).replace(/"/g,"&quot;").replace(/'/g,"&#39;");
            let CONFIG={traceUiTemplate:""};
            const rcls=r=>(r==="Passed"||r==="Succeeded")?"ok":((r==="Aborted"||r==="Failed"||r==="Blocked")?"bad":"run");
            const rtip=r=>({Passed:"The test reached its success condition.",Succeeded:"The run reached its success condition.",Failed:"Reached max steps without meeting the success condition.",Aborted:"Stopped early: score fell below the abort threshold or a quality guard aborted.",Blocked:"Could not start — the target window was not found.",Running:"In progress."}[r]||r);
            async function api(path,opts){
              const res=await fetch(path,opts); const ct=res.headers.get("content-type")||"";
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
              ["catalog","create","runs","live","files"].forEach(t=>$("#tab-"+t).classList.toggle("hidden",t!==tab));
              if(location.hash.slice(1)!==tab) location.hash=tab;
              if(tab!=="live"){ clearInterval(liveTimer); clearInterval(tickTimer); liveTimer=tickTimer=null; }
              if(tab==="catalog")loadCatalog();
              if(tab==="create")loadCreate();
              if(tab==="runs")loadRuns();
              if(tab==="live")loadLive();
              if(tab==="files")loadFiles();
            }
            const headHTML=(title,sub)=>`<div class="head fade"><h2>${title}</h2><span class="sub">${sub}</span></div>`;

            // CATALOG
            async function loadCatalog(){
              const host=$("#tab-catalog");
              host.innerHTML=headHTML("Catalog","Tests discovered under tests/, grouped by suite")+"<div id='cat'></div>";
              try{
                const d=await api("/api/tests");
                const cat=$("#cat");
                if(!d.count){ cat.innerHTML="<div class='empty'>No tests found under tests/. Author one in <b>Create</b>.</div>"; return; }
                const by={}; d.tests.forEach(t=>(by[t.suite||"(no suite)"]||=[]).push(t));
                cat.innerHTML="";
                Object.keys(by).sort().forEach(s=>{
                  const sec=el("div",{className:"fade",style:"margin-bottom:20px"});
                  sec.innerHTML=`<div class="row" style="margin-bottom:9px"><span class="chip">${esc(s)}</span><span class="dim">${by[s].length} test${by[s].length>1?"s":""}</span></div>`;
                  const g=el("div",{className:"grid"}); by[s].forEach(t=>g.appendChild(card(t))); sec.appendChild(g); cat.appendChild(sec);
                });
              }catch(e){ $("#cat").innerHTML=`<div class='empty'>${esc(e.message)}</div>`; }
            }
            function card(t){
              const c=el("div",{className:"card"});
              c.innerHTML=`<h4>${esc(t.id)}</h4><div class="mut" style="font-size:12px;margin-bottom:9px">${esc(t.title||"—")}</div>
                <div class="row" style="margin-bottom:10px">
                  ${t.framework?`<span class="chip fw">${esc(t.framework)}</span>`:""}
                  ${t.priority?`<span class="chip">${esc(t.priority)}</span>`:""}
                  ${(t.tags||[]).slice(0,4).map(x=>`<span class="chip">${esc(x)}</span>`).join("")}
                </div>
                <div class="dim" style="font-size:11.5px;margin-bottom:11px;min-height:32px">${esc(t.goal||"")}</div>`;
              const b=el("button",{className:"act",textContent:"▶ Launch",title:"Spawn the AgentRunner CLI for this test against its target window (needs the app running + a configured LLM). Opens the Live view."}); b.onclick=()=>launch(t); c.appendChild(b);
              return c;
            }
            async function launch(t){
              try{ await api("/api/runs",{method:"POST",headers:{"content-type":"application/json"},
                body:JSON.stringify({planPath:t.planPath,testId:t.id,window:t.targetWindow})}); show("live"); }
              catch(e){ alert("Launch failed: "+e.message); }
            }

            // CREATE
            const F=[
              ["id","Test ID","required","Unique identifier for the test.","DEMO-LOGIN-001"],
              ["suite","Suite","","Group it belongs to (file/folder bucket). Defaults to 'created'.","login"],
              ["title","Title","","Short human-readable name.","Login happy path"],
              ["targetWindow","Target window","","Exact title of the desktop window to drive.","Sample Login App (.NET 8)"],
              ["goal","Goal","required","What the agent must accomplish, in plain language.","Enter admin / password123, click Login, confirm success."],
              ["successCondition","Success condition","","Text the app shows when the goal is met. Leave blank to verify with an Assert instead.","Login successful"]
            ];
            function loadCreate(){
              const fld=([k,l,req,help,ph])=>`<div class="field"><label>${l} ${req?'<span class="req">*</span>':''}</label>
                <span class="help">${help}</span><input id="f-${k}" placeholder="${esc(ph)}"/></div>`;
              $("#tab-create").innerHTML=headHTML("Create a ticket","Writes a validated YAML test under tests/created/ — the YAML stays the source of truth")+
                `<div class="panel pad form fade">
                  ${fld(F[0])}
                  <div class="two">${fld(F[1])}${fld(F[2])}</div>
                  <div class="two">
                    <div class="field"><label>Framework</label><span class="help">Desktop UI toolkit of the target.</span>
                      <select id="f-framework"><option value="">— select —</option><option>winforms</option><option>wpf</option><option>maui</option><option>avalonia</option></select></div>
                    <div class="field"><label>Priority</label><span class="help">Triage importance.</span>
                      <select id="f-priority"><option value="">— select —</option><option>P1</option><option>P2</option><option>P3</option></select></div>
                  </div>
                  ${fld(F[3])}
                  <div class="field"><label>Goal <span class="req">*</span></label><span class="help">${F[4][3]}</span><textarea id="f-goal" rows="2" placeholder="${esc(F[4][4])}"></textarea></div>
                  ${fld(F[5])}
                  <div class="two">
                    <div class="field"><label>Max steps</label><span class="help">Hard cap on agent iterations before failing.</span><input id="f-maxSteps" type="number" value="8"/></div>
                    <div class="field"><label>Allowed actions</label><span class="help">Comma-separated UI actions the agent may use.</span><input id="f-actions" value="EnterText, Click, Assert, Done, Wait"/></div>
                  </div>
                  <div class="field"><label>Tags</label><span class="help">Comma-separated labels for filtering.</span><input id="f-tags" placeholder="smoke, login"/></div>
                  <div class="row" style="margin-top:6px"><button class="act" id="f-save">✓ Validate &amp; save</button><span class="dim" id="f-hint">Validated with the same checker the CLI uses.</span></div>
                  <div id="f-out" style="margin-top:14px"></div>
                </div>`;
              $("#f-save").onclick=saveTest;
            }
            async function saveTest(){
              const v=k=>($("#f-"+k)?.value||"").trim();
              const list=s=>s.split(",").map(x=>x.trim()).filter(Boolean);
              const req={id:v("id"),suite:v("suite"),title:v("title"),framework:v("framework"),priority:v("priority"),
                targetWindow:v("targetWindow"),goal:v("goal"),successCondition:v("successCondition")||null,
                maxSteps:parseInt(v("maxSteps"))||8,allowedActions:list(v("actions")),tags:list(v("tags"))};
              const out=$("#f-out");
              try{ const r=await api("/api/tests",{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify(req)});
                out.innerHTML=`<div class="chip ok" style="margin-bottom:8px">saved ${esc(r.planPath)}</div><pre class="term">${esc(r.yaml)}</pre>`;
              }catch(e){ out.innerHTML=`<div class="chip bad">${esc(e.message)}</div>`; }
            }

            // RUNS
            async function loadRuns(){
              $("#tab-runs").innerHTML=headHTML("Runs","Recorded run history from runs/")+"<div class='panel pad' id='runs-body'></div><div id='run-detail' style='margin-top:14px'></div>";
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
              $("#tab-live").innerHTML=headHTML("Live telemetry",'<span class="dot run" style="display:inline-block;margin-right:6px"></span>polling every 2s · launched runs spawn the CLI (need your target app + .env)')+"<div id='live'></div>";
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
                    const st=stepOf(j.logs||[]); const cls=j.status==="running"?"running":(j.exitCode===0?"ok":"bad");
                    cardEl.className="panel pad live-card "+cls;
                    const key=JSON.stringify([j.status,j.exitCode,j.runId,(j.logs||[]).length,st.cur,st.tot]);
                    if(cardEl.dataset.k!==key){
                      cardEl.dataset.k=key;
                      const dotc=j.status==="running"?"run":(j.exitCode===0?"ok":"bad");
                      cardEl.innerHTML=`
                        <div class="row" style="justify-content:space-between">
                          <div class="row">
                            <span class="dot ${dotc}"></span><b style="letter-spacing:.03em">${esc(j.testId)}</b>
                            <span class="chip ${cls==="running"?"run":cls}">${esc(j.status)}${j.exitCode!=null?(" · exit "+j.exitCode):""}</span>
                          </div>
                          <div class="row">
                            ${j.runId?`<span class="chip info" title="The run id; its artifacts are under runs/${esc(j.runId)}/">run ${esc(j.runId)}</span>`:'<span class="chip" title="Recovered from the runner logs once the run starts">awaiting run id…</span>'}
                            <span class="chip" title="Process id of the spawned AgentRunner CLI">pid ${j.pid}</span>
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

            const validTab=t=>["catalog","create","runs","live","files"].includes(t);
            window.addEventListener("hashchange",()=>{ const t=location.hash.slice(1); if(validTab(t)) show(t); });
            (async()=>{ try{CONFIG=await api("/api/config");}catch{} refreshStatus(); setInterval(refreshStatus,5000);
              show(validTab(location.hash.slice(1))?location.hash.slice(1):"catalog"); })();
          </script>
          <style>.hidden{display:none!important;}</style>
        </body>
        </html>
        """;
}
