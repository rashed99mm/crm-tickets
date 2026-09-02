/**
 * Request Logger Middleware
 * Logs all incoming requests for debugging.
 */

module.exports = function requestLogger() {
    return function (req, res, next) {
        const timestamp = new Date().toISOString();
        console.log(`[${timestamp}] ${req.method} ${req.originalUrl}`);
        
        if (Object.keys(req.query).length > 0) {
            console.log('  Query:', req.query);
        }
        
        if (req.body && Object.keys(req.body).length > 0) {
            console.log('  Body:', JSON.stringify(req.body).substring(0, 500));
        }
        
        next();
    };
};
