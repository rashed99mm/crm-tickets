/**
 * @swagger
 * tags:
 *   name: SMS
 *   description: Twilio Messages mock (CC-36)
 */
const { v4: uuidv4 } = require('uuid');

module.exports = {
    name: 'twilio-gateway',
    group: 'mock',
    description: 'Twilio mock — impersonates POST /2010-04-01/Accounts/{sid}/Messages.json',
    endpoints: [
        {
            path: '/mock/twilio/2010-04-01/Accounts/:accountSid/Messages.json',
            method: 'POST',
            mockDataKey: 'providers-history',
            behaviorKey: 'provider-failure-rules',
            realtimeType: 'sms',
            description: 'Send an SMS (Twilio contract, form-encoded)',
            responseTransform: (req, mockData, rules) => {
                // Twilio takes application/x-www-form-urlencoded with capitalised field names.
                const to = req.body?.To;
                const from = req.body?.From;
                const body = req.body?.Body;

                if (!to || !body) {
                    return {
                        $response: true,
                        status: 400,
                        body: {
                            code: 21604,
                            message: "A 'To' phone number and 'Body' are required",
                            more_info: 'https://www.twilio.com/docs/errors/21604',
                            status: 400,
                        },
                    };
                }

                const failure = rules ? rules.check(to) : null;
                if (failure) {
                    return {
                        $response: true,
                        status: failure.kind === 'permanent' ? 400 : 503,
                        body: {
                            code: failure.kind === 'permanent' ? 21211 : 20500,
                            message: failure.message,
                            more_info: 'https://www.twilio.com/docs/errors',
                            status: failure.kind === 'permanent' ? 400 : 503,
                        },
                    };
                }

                const sid = `SM${uuidv4().replace(/-/g, '')}`;
                return {
                    $response: true,
                    status: 201,
                    body: {
                        sid,
                        account_sid: req.params.accountSid,
                        to,
                        from: from || 'CommandCenter',
                        body,
                        status: 'queued',
                        num_segments: String(Math.ceil(String(body).length / 160)),
                        date_created: new Date().toUTCString(),
                        uri: `/2010-04-01/Accounts/${req.params.accountSid}/Messages/${sid}.json`,
                    },
                };
            },
        },
    ],
};
