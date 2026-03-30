/**
 * uwp_bridge_shim.js
 *
 * Defines window.PtBridge using window.external.notify() so that pt_api.js
 * works identically on UWP WebView as it does with Android's JavascriptInterface.
 *
 * C# → JS: InvokeScriptAsync("eval", [code])
 * JS → C#: window.external.notify(JSON.stringify({m, ...args})) → WebView.ScriptNotify
 *
 * Sync-read methods (getContext, getMessageHeaders) read from globals that C#
 * pre-pushes before each generation via InvokeScriptAsync.
 */
(function () {
    'use strict';

    function notify(obj) {
        try { window.external.notify(JSON.stringify(obj)); } catch (e) { /* ignore */ }
    }

    window.PtBridge = {

        // ── Prompt injection ─────────────────────────────────────────────────

        setPromptInjection: function (id, text, pos, dep) {
            notify({ m: 'setPromptInjection', id: id, text: text, pos: pos, dep: dep,
                     extId: window.__ptCurrentExtId || '' });
        },

        // ── Context (sync read from pre-pushed global) ───────────────────────

        getContext: function () {
            return window.__ptContextJson || '{}';
        },

        // ── Settings ─────────────────────────────────────────────────────────

        saveAllSettings: function (json) {
            notify({ m: 'saveAllSettings', json: json });
        },

        // ── Logging ──────────────────────────────────────────────────────────

        log: function (msg) {
            notify({ m: 'log', msg: String(msg) });
        },

        // ── Quick reply buttons ───────────────────────────────────────────────

        registerButtons: function (id, json) {
            notify({ m: 'registerButtons', id: id, json: json });
        },

        clearButtons: function (id) {
            notify({ m: 'clearButtons', id: id });
        },

        // ── Send message ─────────────────────────────────────────────────────

        sendMessage: function (text) {
            notify({ m: 'sendMessage', text: String(text) });
        },

        // ── Message headers ───────────────────────────────────────────────────

        setMessageHeader: function (idx, text, extId, collapsible) {
            notify({ m: 'setMessageHeader', idx: idx, text: text, extId: extId || '', collapsible: collapsible || '' });
        },

        clearMessageHeader: function (idx) {
            notify({ m: 'clearMessageHeader', idx: idx });
        },

        clearAllHeaders: function () {
            notify({ m: 'clearAllHeaders' });
        },

        getMessageHeaders: function (idx) {
            var h = window.__ptMessageHeaders;
            return (h && h[idx]) ? JSON.stringify(h[idx]) : '[]';
        },

        // ── Header buttons & menus ─────────────────────────────────────────────

        registerHeaderButtons: function (id, json) {
            notify({ m: 'registerHeaderButtons', id: id, json: json });
        },

        clearHeaderButtons: function (id) {
            notify({ m: 'clearHeaderButtons', id: id });
        },

        registerHeaderMenu: function (id, json) {
            notify({ m: 'registerHeaderMenu', id: id, json: json });
        },

        clearHeaderMenu: function (id) {
            notify({ m: 'clearHeaderMenu', id: id });
        },

        // ── Message context actions ────────────────────────────────────────────

        registerMessageActions: function (id, json) {
            notify({ m: 'registerMessageActions', id: id, json: json });
        },

        clearMessageActions: function (id) {
            notify({ m: 'clearMessageActions', id: id });
        },

        // ── Output filters ─────────────────────────────────────────────────────

        registerOutputFilter: function (id, pattern) {
            notify({ m: 'registerOutputFilter', id: id, pattern: pattern });
        },

        clearOutputFilter: function (id) {
            notify({ m: 'clearOutputFilter', id: id });
        },

        // ── Dialogs (async, resolved via __ptEditDialogResult) ─────────────────

        showEditDialog: function (title, fieldsJson, cbId) {
            notify({ m: 'showEditDialog', title: title, fields: fieldsJson, cbId: cbId });
        },

        // ── Hidden generation (async, resolved via __ptHiddenGenerateResult) ────

        generateHidden: function (prompt, cbId) {
            notify({ m: 'generateHidden', prompt: prompt, cbId: cbId });
        },

        // ── Image generation (async, resolved via __ptImageGenerateResult) ───────

        generateImage: function (prompt, optionsJson, cbId) {
            notify({ m: 'generateImage', prompt: prompt, options: optionsJson, cbId: cbId });
        },

        // ── Message insertion ─────────────────────────────────────────────────────

        insertMessage: function (content, optionsJson) {
            notify({ m: 'insertMessage', content: content, options: optionsJson });
        }
    };

    // Pre-initialize globals so sync reads never throw
    window.__ptContextJson    = '{}';
    window.__ptMessageHeaders = {};
    window.__ptDisabledExtensions = [];
    window.__ptCurrentExtId   = null;

})();
