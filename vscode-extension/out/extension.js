"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.activate = activate;
exports.deactivate = deactivate;
const path = require("path");
const vscode = require("vscode");
const node_1 = require("vscode-languageclient/node");
let client;
async function activate(context) {
    const workspaceRoot = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
    const repoRoot = path.dirname(context.extensionPath);
    const compile = vscode.commands.registerCommand('plcflow.compile', async () => {
        const editor = vscode.window.activeTextEditor;
        if (!editor || editor.document.languageId !== 'structured-text') {
            vscode.window.showWarningMessage('Open a Structured Text (.st) file first.');
            return;
        }
        if (!workspaceRoot) {
            vscode.window.showErrorMessage('Open the PLCFlow repository as a VS Code workspace first.');
            return;
        }
        await editor.document.save();
        const terminal = vscode.window.createTerminal('PLCFlow Compiler');
        terminal.show();
        const cliProject = path.join(repoRoot, 'src', 'PLCFlow.Cli', 'PLCFlow.Cli.csproj');
        terminal.sendText(`dotnet run --project "${cliProject}" -- "${editor.document.uri.fsPath}"`);
    });
    context.subscriptions.push(compile);
    if (!workspaceRoot) {
        vscode.window.showWarningMessage('PLCFlow Language Server: open the PLCFlow repository folder to enable language features.');
        return;
    }
    const serverProject = path.join(repoRoot, 'src', 'PLCFlow.LanguageServer', 'PLCFlow.LanguageServer.csproj');
    const serverOptions = {
        command: 'dotnet',
        args: ['run', '--project', serverProject, '--no-launch-profile']
    };
    const clientOptions = {
        documentSelector: [{ scheme: 'file', language: 'structured-text' }],
        synchronize: {
            fileEvents: vscode.workspace.createFileSystemWatcher('**/*.st')
        }
    };
    client = new node_1.LanguageClient('plcflowLanguageServer', 'PLCFlow Language Server', serverOptions, clientOptions);
    context.subscriptions.push(client);
    await client.start();
    vscode.window.setStatusBarMessage('$(check) PLCFlow Language Server ready', 3000);
}
async function deactivate() {
    if (client)
        await client.stop();
}
//# sourceMappingURL=extension.js.map