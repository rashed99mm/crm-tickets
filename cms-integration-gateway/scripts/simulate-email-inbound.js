#!/usr/bin/env node
/**
 * Simulates SendGrid Inbound Parse delivering an inbound email to the backend (spec A26, proving
 * CC-42/CC-43). Unsigned by design: Inbound Parse does not sign its posts (spec A21).
 *
 * Usage:
 *   npm run simulate:email
 *   npm run simulate:email -- --from "Layla <layla@example.com>" --subject "Refund"
 *   npm run simulate:email -- --twice        # same Message-ID twice, proving CC-43
 */
const crypto = require('crypto');
const config = require('../config');

function arg(name, fallback) {
    const i = process.argv.indexOf(`--${name}`);
    return i !== -1 && process.argv[i + 1] ? process.argv[i + 1] : fallback;
}

const from = arg('from', '"Layla Haddad" <layla@example.com>');
const subject = arg('subject', 'Refund not received');
const text = arg('text', 'I was told the refund would arrive last week.');
const messageId = arg('id', `<${crypto.randomUUID()}@mail.example.com>`);
const twice = process.argv.includes('--twice');

const url = `${config.CALLBACK_BASE_URL.replace(/\/$/, '')}/api/channels/email/webhook`;

/** Inbound Parse posts multipart/form-data with these field names. */
function buildForm() {
    const form = new FormData();
    form.append(
        'headers',
        [
            'Received: by mx.sendgrid.net with SMTP',
            `Message-ID: ${messageId}`,
            `From: ${from}`,
            `Subject: ${subject}`,
        ].join('\r\n'),
    );
    form.append('from', from);
    form.append('to', 'support@example.com');
    form.append('subject', subject);
    form.append('text', text);
    form.append('envelope', JSON.stringify({ to: ['support@example.com'], from }));
    form.append('charsets', JSON.stringify({ text: 'UTF-8', subject: 'UTF-8' }));
    form.append('SPF', 'pass');
    return form;
}

async function post(attempt) {
    const response = await fetch(url, { method: 'POST', body: buildForm() });
    console.log(`  attempt ${attempt} -> ${response.status} ${response.statusText}`);
    if (response.status !== 200) {
        console.error('FAIL: expected 200');
        process.exit(1);
    }
}

async function main() {
    console.log(`POST ${url}`);
    console.log(`  from=${from} Message-ID=${messageId}`);

    await post(1);
    if (twice) {
        // CC-43: the same Message-ID must not create a second TicketMessage. The response is
        // identical either way; check the database (or the ticket timeline) to see one row.
        await post(2);
        console.log('  posted twice with one Message-ID — CC-43 expects exactly one stored message');
    }

    console.log('OK');
}

main().catch((error) => {
    console.error(`FAIL: ${error.message}`);
    console.error('Is the ExternalApi host running at', config.CALLBACK_BASE_URL, '?');
    process.exit(1);
});
