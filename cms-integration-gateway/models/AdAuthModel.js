/**
 * @swagger
 * tags:
 *   name: Auth
 *   description: Active Directory authentication mock operations
 */

const jwt = require('jsonwebtoken');
const fs = require('fs');
const path = require('path');

const JWT_SECRET = process.env.JWT_SECRET || 'cce-carbon-mock-secret-key-2024';
const JWT_EXPIRES_IN = '24h';
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

function generateToken(user, username) {
    const payload = {
        email: username,
        firstName: user.firstName,
        lastName: user.lastName,
        displayName: user.displayName,
        groups: user.groups,
        iat: Math.floor(Date.now() / 1000)
    };
    return jwt.sign(payload, JWT_SECRET, { expiresIn: JWT_EXPIRES_IN });
}

module.exports = {
    name: 'ad-auth',
    group: 'integrationgateway',
    description: 'Mock Active Directory authentication gateway',
    endpoints: [
        {
            path: '/integrationgateway/auth/ad/login',
            method: 'POST',
            mockDataKey: 'auth-ad-users',
            behaviorKey: 'ad-auth-rules',
            description: 'Authenticate user via Active Directory',
            /**
             * @swagger
             * /integrationgateway/auth/ad/login:
             *   post:
             *     summary: AD Login
             *     tags: [Auth]
             *     requestBody:
             *       required: true
             *       content:
             *         application/json:
             *           schema:
             *             type: object
             *             properties:
             *               username:
             *                 type: string
             *                 example: "admin@company.com"
             *               password:
             *                 type: string
             *                 example: "P@ssw0rd123"
             *     responses:
             *       200:
             *         description: Authentication result
             */
            responseTransform: (req, mockData, rules) => {
                const payload = req.body || {};
                const validationError = rules ? rules.validate(payload) : null;

                if (validationError) {
                    return {
                        status: validationError.status,
                        email: payload.username || null,
                        firstName: null,
                        lastName: null,
                        displayName: null,
                        groups: [],
                        token: null,
                        error: validationError.error
                    };
                }

                const username = payload.username.trim().toLowerCase();
                const users = loadUsers();
                const user = users[username];

                const token = generateToken(user, username);

                return {
                    status: 'success',
                    email: username,
                    firstName: user.firstName,
                    lastName: user.lastName,
                    displayName: user.displayName,
                    groups: user.groups,
                    token: token,
                    error: null
                };
            }
        },
        {
            path: '/integrationgateway/auth/ad/me',
            method: 'GET',
            mockDataKey: 'auth-ad-users',
            description: 'Get current authenticated user profile (mock)',
            /**
             * @swagger
             * /integrationgateway/auth/ad/me:
             *   get:
             *     summary: Get current user profile
             *     tags: [Auth]
             *     responses:
             *       200:
             *         description: User profile
             */
            responseTransform: (req, mockData, rules) => {
                // For mock purposes, return the first enabled user
                // In real scenario, this would decode the Authorization header token
                const users = loadUsers();
                const firstUserKey = Object.keys(users).find(key => users[key].enabled !== false);

                if (!firstUserKey) {
                    return {
                        status: 'failed',
                        error: {
                            code: 'UNAUTHORIZED',
                            message: 'No authenticated user found.'
                        }
                    };
                }

                const user = users[firstUserKey];

                return {
                    status: 'success',
                    email: firstUserKey,
                    firstName: user.firstName,
                    lastName: user.lastName,
                    displayName: user.displayName,
                    groups: user.groups,
                    error: null
                };
            }
        }
    ]
};
