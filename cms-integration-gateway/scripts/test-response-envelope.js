// scripts/test-response-envelope.js — run against a started server (npm start on port 3001).
const http = require('http');

function post(path, body, contentType = 'application/json') {
    return new Promise((resolve) => {
        const payload = contentType === 'application/json' ? JSON.stringify(body) : body;
        const req = http.request(
            { hostname: 'localhost', port: 3001, path, method: 'POST',
              headers: { 'Content-Type': contentType, 'Content-Length': Buffer.byteLength(payload) } },
            (res) => {
                let data = '';
                res.on('data', (c) => (data += c));
                res.on('end', () => resolve({ status: res.statusCode, headers: res.headers, body: data }));
            });
        req.write(payload);
        req.end();
    });
}

(async () => {
    const sendgrid = await post('/mock/sendgrid/v3/mail/send', {
        personalizations: [{ to: [{ email: 'customer@example.com' }] }],
        from: { email: 'support@commandcenter.local' },
        subject: 'Hello',
        content: [{ type: 'text/plain', value: 'Body' }],
    });

    const meta = await post('/mock/meta/v18.0/100000000000000/messages', {
        messaging_product: 'whatsapp', to: '+15559998888', type: 'text', text: { body: 'hi' },
    });

    const twilio = await post(
        '/mock/twilio/2010-04-01/Accounts/ACmockaccountsid/Messages.json',
        'To=%2B15559998888&From=CommandCenter&Body=hi',
        'application/x-www-form-urlencoded');

    const checks = [
        ['sendgrid status is 202', sendgrid.status === 202],
        ['sendgrid sets x-message-id', Boolean(sendgrid.headers['x-message-id'])],
        ['sendgrid body is empty', sendgrid.body === ''],
        ['meta answers 200', meta.status === 200],
        ['meta returns a wamid', /^wamid\./.test((JSON.parse(meta.body || '{}').messages || [])[0]?.id || '')],
        ['twilio answers 201', twilio.status === 201],
        ['twilio returns an SM sid', /^SM/.test(JSON.parse(twilio.body || '{}').sid || '')],
        ['legacy sms route still answers 200 json',
            (await post('/integrationgateway/sms/send', { to: '+966501234567', body: 'hi' })).status === 200],
    ];

    let failed = 0;
    for (const [name, ok] of checks) {
        console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}`);
        if (!ok) failed += 1;
    }
    process.exit(failed === 0 ? 0 : 1);
})();
