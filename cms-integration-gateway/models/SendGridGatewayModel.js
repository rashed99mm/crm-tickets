/**
 * @swagger
 * tags:
 *   name: SendGrid
 *   description: SendGrid v3 mail/send mock (CC-34)
 */
const { v4: uuidv4 } = require('uuid');

module.exports = {
    name: 'sendgrid-gateway',
    group: 'mock',
    description: 'SendGrid v3 mock — impersonates POST /v3/mail/send',
    endpoints: [
        {
            path: '/mock/sendgrid/v3/mail/send',
            method: 'POST',
            mockDataKey: 'providers-history',
            behaviorKey: 'provider-failure-rules',
            realtimeType: 'email',
            description: 'Send an email (SendGrid v3 contract)',
            responseTransform: (req, mockData, rules) => {
                const payload = req.body || {};
                const to = payload?.personalizations?.[0]?.to?.[0]?.email || null;

                if (!to || !payload.from?.email || !payload.subject) {
                    // SendGrid's real validation envelope.
                    return {
                        $response: true,
                        status: 400,
                        body: { errors: [{ message: 'missing required field', field: 'personalizations.to' }] },
                    };
                }

                const failure = rules ? rules.check(to) : null;
                if (failure) {
                    return {
                        $response: true,
                        status: failure.kind === 'permanent' ? 400 : 503,
                        body: { errors: [{ message: failure.message, field: null, help: failure.code }] },
                    };
                }

                // 202 Accepted, empty body, id in a header — the real contract.
                return {
                    $response: true,
                    status: 202,
                    headers: { 'X-Message-Id': `sg-${uuidv4()}` },
                    body: null,
                };
            },
        },
    ],
};
