/**
 * @swagger
 * tags:
 *   name: WhatsApp
 *   description: Meta WhatsApp Cloud API mock (CC-35)
 */
const { v4: uuidv4 } = require('uuid');

module.exports = {
    name: 'meta-whatsapp-gateway',
    group: 'mock',
    description: 'Meta Cloud API v18 mock — impersonates POST /{phone-number-id}/messages',
    endpoints: [
        {
            path: '/mock/meta/v18.0/:phoneNumberId/messages',
            method: 'POST',
            mockDataKey: 'providers-history',
            behaviorKey: 'provider-failure-rules',
            realtimeType: 'whatsapp',
            description: 'Send a WhatsApp message (Cloud API contract)',
            responseTransform: (req, mockData, rules) => {
                const payload = req.body || {};

                if (payload.messaging_product !== 'whatsapp' || !payload.to) {
                    // Meta's real error envelope.
                    return {
                        $response: true,
                        status: 400,
                        body: {
                            error: {
                                message: '(#100) Invalid parameter',
                                type: 'OAuthException',
                                code: 100,
                                fbtrace_id: uuidv4(),
                            },
                        },
                    };
                }

                const failure = rules ? rules.check(payload.to) : null;
                if (failure) {
                    return {
                        $response: true,
                        status: failure.kind === 'permanent' ? 400 : 503,
                        body: {
                            error: {
                                message: failure.message,
                                type: 'OAuthException',
                                code: failure.kind === 'permanent' ? 131026 : 500,
                                fbtrace_id: uuidv4(),
                            },
                        },
                    };
                }

                return {
                    $response: true,
                    status: 200,
                    body: {
                        messaging_product: 'whatsapp',
                        contacts: [{ input: payload.to, wa_id: String(payload.to).replace(/\D/g, '') }],
                        messages: [{ id: `wamid.${Buffer.from(uuidv4()).toString('base64').replace(/=+$/, '')}` }],
                    },
                };
            },
        },
    ],
};
