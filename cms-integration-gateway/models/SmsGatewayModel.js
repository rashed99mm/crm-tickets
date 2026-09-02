/**
 * @swagger
 * tags:
 *   name: SMS
 *   description: SMS gateway mock operations
 */

const { v4: uuidv4 } = require('uuid');

module.exports = {
    name: 'sms-gateway',
    group: 'integrationgateway',
    description: 'Mock SMS provider gateway for CommandCenter CMS social media & business platform',
    endpoints: [
        {
            path: '/integrationgateway/sms/send',
            method: 'POST',
            mockDataKey: 'sms-responses',
            behaviorKey: 'sms-rules',
            description: 'Send an SMS message',
            /**
             * @swagger
             * /integrationgateway/sms/send:
             *   post:
             *     summary: Send an SMS
             *     tags: [SMS]
             *     requestBody:
             *       required: true
             *       content:
             *         application/json:
             *           schema:
             *             type: object
             *             properties:
             *               to:
             *                 type: string
             *                 example: "+966501234567"
             *               from:
             *                 type: string
              *                 example: "CommandCenter"
              *               body:
              *                 type: string
              *                 example: "Welcome to CommandCenter CMS! Your OTP is 123456"
              *               templateId:
              *                 type: string
              *                 example: "otp-en"
              *     responses:
              *       200:
              *         description: SMS sent successfully or failed
             */
            responseTransform: (req, mockData, rules) => {
                const payload = req.body || {};
                if (payload.To && !payload.to) payload.to = payload.To;
                if (payload.Message && !payload.body) payload.body = payload.Message;
                if (payload.message && !payload.body) payload.body = payload.message;
                if (payload.From && !payload.from) payload.from = payload.From;
                const ruleResult = rules ? rules.check(payload) : null;

                if (ruleResult) {
                    return {
                        status: 'failed',
                        messageId: null,
                        to: payload.to || null,
                        error: {
                            code: ruleResult.code,
                            message: ruleResult.message
                        },
                        timestamp: new Date().toISOString()
                    };
                }

                    return {
                        status: 'success',
                        message: 'SMS sent successfully',
                        messageId: `SMS-${uuidv4()}`,
                        to: payload.to,
                        from: payload.from || 'CommandCenter',
                        body: payload.body || null,
                        timestamp: new Date().toISOString()
                    };
            }
        },
        {
            path: '/integrationgateway/sms/status/:messageId',
            method: 'GET',
            mockDataKey: 'sms-history',
            description: 'Get SMS delivery status',
            /**
             * @swagger
             * /integrationgateway/sms/status/{messageId}:
             *   get:
             *     summary: Get SMS status
             *     tags: [SMS]
             *     parameters:
             *       - in: path
             *         name: messageId
             *         required: true
             *         schema:
             *           type: string
             *     responses:
             *       200:
             *         description: Delivery status
             */
            responseTransform: (req, mockData, rules) => {
                const messageId = req.params.messageId;
                const history = mockData || [];
                const record = history.find(h => h.messageId === messageId);

                if (record) {
                    return record;
                }

                // Simulate status progression
                const statuses = ['sent', 'delivered'];
                const randomStatus = statuses[Math.floor(Math.random() * statuses.length)];

                return {
                    messageId,
                    status: randomStatus,
                    to: '+966501234567',
                    updatedAt: new Date().toISOString()
                };
            }
        },
        {
            path: '/integrationgateway/sms/templates',
            method: 'GET',
            mockDataKey: 'sms-templates',
            description: 'List available SMS templates',
            /**
             * @swagger
             * /integrationgateway/sms/templates:
             *   get:
             *     summary: List SMS templates
             *     tags: [SMS]
             *     responses:
             *       200:
             *         description: List of templates
             */
            responseTransform: (req, mockData, rules) => {
                return {
                    success: true,
                    data: mockData || [],
                    count: (mockData || []).length
                };
            }
        }
    ]
};
