#!/usr/bin/env python3
"""Integration checks against the open Editor. Run in Play mode; restores time/audio on exit."""
import json
import subprocess
import time
import sys
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
REPORT = PROJECT / 'QA' / 'polish-results.json'
results = []
KEYBOARD = "--keyboard" in sys.argv

def cli(name, **params):
    args = ['unity', 'command', name, '--caller', 'plugin', '--skill', 'unity-cli',
            '--project-path', str(PROJECT), '--format', 'json', '--']
    for key, val in params.items():
        args += ['--' + key, str(val)]
    r = subprocess.run(args, cwd=PROJECT, capture_output=True, text=True, timeout=40)
    data = json.loads(r.stdout)
    assert data.get('success'), data
    value = data['data']['result']
    if isinstance(value, dict) and value.get('success') is False:
        raise AssertionError(value)
    return value

def ev(code):
    return cli('eval', code=code).get('result')

def check(name, condition, detail):
    results.append({'check': name, 'passed': bool(condition), 'detail': detail})
    print(('PASS ' if condition else 'FAIL ') + name + ': ' + str(detail), flush=True)
    assert condition, (name, detail)

def shot(name, width=1600, height=900):
    return cli('capture_game_view', source='camera', width=width, height=height, save_path='QA/' + name + '.png')

START = 'typeof(SlackGame).GetMethod("StartRound",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(SlackGame.I,null);'
STATE = 'var g=SlackGame.I; return g.State+"|"+g.Slack.ToString("F1")+"|"+g.Lives+"|"+g.PraiseCount+"|"+g.TimeLeft.ToString("F1")+"|"+OfficeBuilder.Boss.State;'

def key(name):
    # CLI eval events can be consumed by the Editor input loop while Game View is
    # unfocused. Keep native keyboard coverage opt-in, and use game commands for CI.
    if not KEYBOARD:
        if name == 'Escape':
            ev('SlackGame.I.SetPaused(!SlackGame.I.IsPaused); return true;')
        elif ev('return SlackGame.I.State.ToString();') == 'Title':
            ev(START + 'return true;')
        else:
            ev('typeof(SlackGame).GetMethod("DoToggle",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(SlackGame.I,null); return true;')
        return
    ev('UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.' + name + ')); return true;')
    ev('UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,new UnityEngine.InputSystem.LowLevel.KeyboardState()); return true;')

try:
    ev('typeof(SlackGame).GetMethod("EnterTitle",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(SlackGame.I,null); return true;')
    time.sleep(0.4)
    shot('title-final')
    key('Space')
    check(('Keyboard' if KEYBOARD else 'Game command') + ' starts round', ev('return SlackGame.I.State.ToString();') == 'Playing', ev(STATE))
    key('Space')
    check(('Keyboard' if KEYBOARD else 'Game command') + ' switches to work', ev('return !SlackGame.I.IsSlacking;'), ev(STATE))
    shot('work-final')
    key('Escape')
    before = ev('return SlackGame.I.TimeLeft;')
    time.sleep(0.3)
    after = ev('return SlackGame.I.TimeLeft;')
    check('Pause freezes timer and audio', before == after and ev('return SlackGame.I.IsPaused && UnityEngine.AudioListener.pause;'), [before, after])
    shot('pause-final')
    key('Escape')
    check('Resume restores simulation', ev('return !SlackGame.I.IsPaused && Time.timeScale==1f && !UnityEngine.AudioListener.pause;'), ev(STATE))

    # Restart, and use actual boss states to decide when to switch. A short reaction delay
    # exercises the warning window rather than overwriting score/lives to fabricate a win.
    ev(START + 'UnityEngine.Random.InitState(124); Time.timeScale=3f; return true;')
    ev('''var g=SlackGame.I; var b=OfficeBuilder.Boss;
        var toggle=typeof(SlackGame).GetMethod("DoToggle",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        float reactAt=-1f; double deadline=UnityEditor.EditorApplication.timeSinceStartup+45;
        UnityEditor.EditorApplication.CallbackFunction driver=null;
        driver=()=>{
            if(g==null || g.State==SlackGame.GameState.Win || g.State==SlackGame.GameState.Lose || UnityEditor.EditorApplication.timeSinceStartup>deadline) {
                UnityEditor.EditorApplication.update-=driver; return;
            }
            bool danger=b.State==BossController.BState.Warn || b.State==BossController.BState.Watch;
            if(danger && reactAt<0f) reactAt=Time.time+0.18f;
            if(!danger) reactAt=-1f;
            bool desired=!danger || Time.time<reactAt;
            if(g.State==SlackGame.GameState.Playing && g.IsSlacking!=desired) toggle.Invoke(g,null);
        };
        UnityEditor.EditorApplication.update+=driver; return true;''')
    deadline = time.monotonic() + 45
    final = None
    while time.monotonic() < deadline:
        final = ev(STATE)
        if final.startswith(('Win|', 'Lose|')):
            break
        time.sleep(0.5)
    check('Natural state-driven play can win', final.startswith('Win|'), final)
    check('Timely switching earns praise', int(final.split('|')[3]) > 0, final)
    shot('win-final')

    reset = ev(START + '''Time.timeScale=0f;
        var s=UnityEngine.Object.FindAnyObjectByType<ShooterScreen>();
        return SlackGame.I.Slack==0f && SlackGame.I.Lives==3 && SlackGame.I.PraiseCount==0 &&
            SlackGame.I.TimeLeft==75f && SlackGame.I.Flow==0f && s.Kills==0 && s.Score==0 && s.Hp==100;''')
    check('Restart resets both games', reset, reset)
    # Zero score + hidden boss isolates timeout from being caught.
    ev('OfficeBuilder.Boss.Halt(); OfficeBuilder.Screen.SetSlacking(false); Time.timeScale=20f; return true;')
    deadline = time.monotonic() + 12
    while time.monotonic() < deadline:
        final = ev(STATE)
        if final.startswith('Lose|'):
            break
        time.sleep(0.25)
    check('Timer expires into failure', final.startswith('Lose|') and final.split('|')[2]=='3', final)
    shot('timeout-final')

    ev(START + 'UnityEngine.Random.InitState(44); Time.timeScale=3f; return true;')
    states = set()
    captured_caught = False
    deadline = time.monotonic() + 25
    while time.monotonic() < deadline:
        final = ev(STATE)
        boss = final.split('|')[5]
        states.add(boss)
        if final.startswith('Caught|') and not captured_caught:
            ev('Time.timeScale=0f; return true;')
            shot('caught-final')
            ev('Time.timeScale=3f; return true;')
            captured_caught = True
        if final.startswith(('Win|', 'Lose|')):
            break
        time.sleep(0.2)
    check('Ignoring boss loses all lives', final.startswith('Lose|') and final.split('|')[2]=='0', final)
    check('Caught sequence progresses', captured_caught, sorted(states))

    # Finish with a fresh playable round, capture alternative ratios, then restore a title screen.
    ev(START + 'Time.timeScale=1f; return true;')
    time.sleep(0.7)
    ev('Time.timeScale=0f; OfficeBuilder.Boss.EditorPreview(); return true;')
    shot('gameplay-final')
    shot('gameplay-4x3', 1200, 900)
    shot('gameplay-wide', 1920, 810)
    console = cli('console', tail=100, level='error')
    game_errors = [e for e in console['entries'] if 'Assets/Scripts/' in e.get('stackTrace','') or 'Exception' in e.get('message','')]
    check('No game exceptions during playtests', not game_errors, game_errors)
finally:
    ev('if(SlackGame.I!=null && SlackGame.I.IsPaused) SlackGame.I.SetPaused(false); Time.timeScale=1f; UnityEngine.AudioListener.pause=false; if(SlackGame.I!=null) typeof(SlackGame).GetMethod("EnterTitle",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(SlackGame.I,null); return true;')
    REPORT.write_text(json.dumps(results, ensure_ascii=False, indent=2))
    print('Report:', REPORT, flush=True)
