/**
 * Email Behavior Rules
 * Define custom success/failure logic for Email gateway.
 */

module.exports = {
    /**
     * Check if the Email request should fail based on rules.
     * @param {Object} payload - Request body
     * @returns {Object|null} - Error object if fails, null if success
     */
    check: (payload) => {
        const email = (payload.to || '').trim().toLowerCase();

        if (email.startsWith('bounce@')) {
            return {
                code: 'BOUNCE',
                message: 'Mailbox does not exist.'
            };
        }

        if (email.includes('spam')) {
            return {
                code: 'SPAM_DETECTED',
                message: 'Message flagged as spam by recipient server.'
            };
        }

        if (!email.includes('@') || !email.includes('.')) {
            return {
                code: 'INVALID_EMAIL',
                message: 'Invalid email format.'
            };
        }

        return null; // success
    }
};
