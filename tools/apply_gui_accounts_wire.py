#!/usr/bin/env python3
from pathlib import Path
import sys
p = Path('src/MailLoadTester.Gui/MainForm.cs')
t = p.read_text()
if 'InitAccountsTab' not in t:
    marker = '        tabs.TabPages.Add(tabSmtp);\n\n        // --- Tab: Zpráva ---'
    if marker in t:
        t = t.replace(marker, '        tabs.TabPages.Add(tabSmtp);\n        InitAccountsTab(tabs);\n\n        // --- Tab: Zpráva ---', 1)
    else:
        t = t.replace('        tabs.TabPages.Add(tabSmtp);', '        tabs.TabPages.Add(tabSmtp);\n        InitAccountsTab(tabs);', 1)
    print('InitAccountsTab')
if 'BuildAccountsFromUi' not in t:
    old = '''            Unauthorized: !chkTestMode.Checked
                || AuthorizationGate.HasUnauthorizedFlag(Environment.GetCommandLineArgs()));
    }'''
    new = '''            Unauthorized: !chkTestMode.Checked
                || AuthorizationGate.HasUnauthorizedFlag(Environment.GetCommandLineArgs()),
            Accounts: BuildAccountsFromUi());
    }'''
    if old not in t:
        sys.exit('BuildOptions tail not found')
    t = t.replace(old, new, 1)
    print('BuildOptions')
p.write_text(t)
print('done')
