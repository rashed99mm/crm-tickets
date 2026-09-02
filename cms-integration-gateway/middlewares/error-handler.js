/**
 * Global Error Handler Middleware
 */

const config = require('../config');

module.exports = function errorHandler(err, req, res, next) {
    console.error('Error:', err.stack || err.message);
    
    res.status(err.status || 500).json({
        ...config.ERROR_RESPONSE_FORMAT,
        message: err.message || 'Internal Server Error',
        code: err.code || 'INTERNAL_ERROR'
    });
};
