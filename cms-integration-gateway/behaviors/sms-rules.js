/**
 * SMS Behavior Rules
 * Define custom success/failure logic for SMS gateway.
 */

module.exports = {
    /**
     * Check if the SMS request should fail based on rules.
     * @param {Object} payload - Request body
     * @returns {Object|null} - Error object if fails, null if success
     */
    check: (payload) => {
        let phone = (payload.to || '').trim();

        if (!phone.startsWith('+')) {
            phone = '+' + phone;
            payload.to = phone;
        }

        if (phone.endsWith('000')) {
            return {
                code: 'INVALID_PHONE_NUMBER',
                message: `The phone number ${phone} is invalid.`
            };
        }

        if (phone.includes('999')) {
            return {
                code: 'CARRIER_BLOCKED',
                message: 'Carrier blocked this number.'
            };
        }

        return null; // success
    }
};
