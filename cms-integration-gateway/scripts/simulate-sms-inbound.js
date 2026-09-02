#!/usr/bin/env node
/**
 * Simulates Twilio delivering an inbound SMS to the backend (spec A26, proving CC-40/CC-41).
 *
 * The gateway plays the provider here, so this posts OUT to the backend's ExternalApi host at
 * CALLBACK_BASE_URL — it is not a route this server hosts.
 *
 * Usage:
 *   npm run simulate:sms
 *   npm run simulate:sms -- --from +15551230001 --body "where is my order"
 *   npm run simulate:sms -- --unsigned          # expect 401 (CC-41)
 */
const crypto = require('crypto');
const config = require('../config');

function arg(name, fallback) {
    const i = process.argv.indexOf(`--${name}`);
    return i !== -1 && process.argv[i + 1] ? process.argv[i + 1] : fallback;
}

const from = arg('from', '+15551230001');
const body = arg('body', 'Hello from the inbound SMS simulator');
const messageSid = arg('sid', `SM${crypto.randomBytes(16).toString('hex')}`);
const unsigned = process.argv.includes('--unsigned');

const url = `${config.CALLBACK_BASE_URL.replace(/\/$/, '')}/api/channels/sms/webhook`;
const form = { Body: body, From: from, MessageSid: messageSid, To: '+15550000000' };

/**
 * Twilio's scheme: the URL, then every parameter's key immediately followed by its value in
 * alphabetical key order, HMAC-SHA1 with the auth token, Base64. Must match
 * TwilioSignatureVerifier.Compute exactly — a mismatch is a 401, which is the useful signal.
 */
function sign(secret, signedUrl, params) {
    const payload = Object.keys(params)
        .sort()
        .reduce((acc, key) => acc + key + params[key], signedUrl);
    return crypto.createHmac('sha1', secret).update(payload, 'utf8').digest('base64');
}

async function main() {
    const headers = { 'Content-Type': 'application/x-www-form-urlencoded' };
    if (!unsigned) {
        headers['X-Twilio-Signature'] = sign(config.WEBHOOK_SECRET, url, form);
    }

    console.log(`POST ${url}`);
    console.log(`  From=${from} MessageSid=${messageSid} signed=${!unsigned}`);

    const response = await fetch(url, {
        method: 'POST',
        headers,
        body: new URLSearchParams(form).toString(),
    });

    console.log(`  -> ${response.status} ${response.statusText}`);
    const text = await response.text();
    if (text) {
        console.log(`  -> ${text.slice(0, 400)}`);
    }

    // 401 is the correct, expected answer for --unsigned; anything else unexpected is a failure.
    const expected = unsigned ? 401 : 200;
    if (response.status !== expected) {
        console.error(`FAIL: expected ${expected}`);
        process.exit(1);
    }
    console.log('OK');
}

main().catch((error) => {
    console.error(`FAIL: ${error.message}`);
    console.error('Is the ExternalApi host running at', config.CALLBACK_BASE_URL, '?');
    process.exit(1);
});
