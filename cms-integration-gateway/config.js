const path = require('path');

const config = {
    PORT: process.env.PORT || 3001,
    NODE_ENV: process.env.NODE_ENV || 'development',
    MOCKS_DIR: path.resolve(__dirname, 'mocks/'),
    ROUTES_FILE: path.join(__dirname, 'routes.json'),
    LOCALE: process.env.LOCALE || 'en-SA',
    LOG_LEVEL: process.env.LOG_LEVEL || 'info',
    CORS_ENABLED: process.env.CORS_ENABLED !== 'false',
    CALLBACK_BASE_URL: process.env.CALLBACK_BASE_URL || 'http://localhost:5095',
    WEBHOOK_SECRET: process.env.WEBHOOK_SECRET || 'dev-only-channel-webhook-secret',
    RATE_LIMIT: {
        windowMs: 15 * 60 * 1000,
        max: 1000
    },
    COMPANY_NAME: 'CommandCenter CMS',
    COMPANY_TAGLINE: 'Customer Management System Integration Gateway',
    ERROR_RESPONSE_FORMAT: {
        status: 'error',
        message: '',
        code: ''
    }
};

module.exports = config;
