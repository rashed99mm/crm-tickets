/**
 * AD Auth Behavior Rules
 * Define validation scenarios for Active Directory authentication.
 */

const fs = require('fs');
const path = require('path');

const USERS_FILE = path.join(__dirname, '..', 'mocks', 'auth', 'ad-users.json');

function loadUsers() {
    try {
        if (fs.existsSync(USERS_FILE)) {
            const data = JSON.parse(fs.readFileSync(USERS_FILE, 'utf8'));
            return data.users || {};
        }
    } catch (err) {
        console.error('[AD Auth] Error loading users:', err.message);
    }
    return {};
}

module.exports = {
    /**
     * Validate AD login credentials
     * @param {Object} payload - { username, password }
     * @returns {Object|null} - Error object if validation fails, null if success
     */
    validate: (payload) => {
        const username = (payload.username || '').trim().toLowerCase();
        const password = payload.password || '';

        // Scenario 1: Missing credentials
        if (!username || !password) {
            return {
                status: 'failed',
                error: {
                    code: 'MISSING_CREDENTIALS',
                    message: 'Username and password are required.'
                }
            };
        }

        const users = loadUsers();
        const user = users[username];

        // Scenario 2: Invalid username (user not found)
        if (!user) {
            return {
                status: 'failed',
                error: {
                    code: 'INVALID_CREDENTIALS',
                    message: 'Invalid username or password.'
                }
            };
        }

        // Scenario 3: Account disabled
        if (user.enabled === false) {
            return {
                status: 'failed',
                error: {
                    code: 'ACCOUNT_DISABLED',
                    message: 'Your account has been disabled. Contact your administrator.'
                }
            };
        }

        // Scenario 4: Wrong password
        if (user.password !== password) {
            return {
                status: 'failed',
                error: {
                    code: 'INVALID_CREDENTIALS',
                    message: 'Invalid username or password.'
                }
            };
        }

        // Success
        return null;
    },

    /**
     * Get user profile by username
     * @param {string} username
     * @returns {Object|null} - User profile or null
     */
    getUser: (username) => {
        const users = loadUsers();
        const user = users[(username || '').trim().toLowerCase()];
        if (!user || user.enabled === false) return null;
        return user;
    }
};
