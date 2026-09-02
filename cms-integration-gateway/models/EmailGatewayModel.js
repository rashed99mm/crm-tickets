/**
 * @swagger
 * tags:
 *   name: Email
 *   description: Email gateway mock operations
 */

const { v4: uuidv4 } = require('uuid');

module.exports = {
    name: 'email-gateway',
    group: 'integrationgateway',
    description: 'Mock Email provider gateway for CommandCenter CMS social media & business platform',
    endpoints: [
        {
            path: '/integrationgateway/email/send',
            method: 'POST',
            mockDataKey: 'email-responses',
            behaviorKey: 'email-rules',
            description: 'Send an email',
            /**
             * @swagger
             * /integrationgateway/email/send:
             *   post:
             *     summary: Send an Email
             *     tags: [Email]
             *     requestBody:
             *       required: true
             *       content:
             *         application/json:
             *           schema:
             *             type: object
             *             properties:
             *               to:
             *                 type: string
             *                 example: "user@example.com"
             *               from:
             *                 type: string
             *                 example: "noreply@azm.sa"
             *               subject:
             *                 type: string
              *                 example: "Welcome to CommandCenter CMS"
              *               html:
              *                 type: string
              *                 example: "<h1>Welcome to CommandCenter CMS</h1><p>Join our sustainability community today!</p>"
              *               templateId:
              *                 type: string
              *                 example: "welcome-email"
              *     responses:
              *       200:
              *         description: Email sent successfully or failed
             */
            responseTransform: (req, mockData, rules) => {
                const payload = req.body || {};
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
                    message: 'Email sent successfully',
                    messageId: `EML-${uuidv4()}`,
                    to: payload.to,
                    from: payload.from || 'noreply@ccecarbon.com',
                    subject: payload.subject,
                    timestamp: new Date().toISOString()
                };
            }
        },
        {
            path: '/integrationgateway/email/status/:messageId',
            method: 'GET',
            mockDataKey: 'email-history',
            description: 'Get email delivery status',
            /**
             * @swagger
             * /integrationgateway/email/status/{messageId}:
             *   get:
             *     summary: Get Email status
             *     tags: [Email]
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

                const statuses = ['sent', 'delivered'];
                const randomStatus = statuses[Math.floor(Math.random() * statuses.length)];

                return {
                    messageId,
                    status: randomStatus,
                    to: 'user@example.com',
                    updatedAt: new Date().toISOString()
                };
            }
        },
        {
            path: '/integrationgateway/email/templates',
            method: 'GET',
            mockDataKey: 'email-templates',
            description: 'List available email templates',
            /**
             * @swagger
             * /integrationgateway/email/templates:
             *   get:
             *     summary: List Email templates
             *     tags: [Email]
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
