/**
 * Generic Gateway Handler
 * Reads service models from the registry and auto-creates Express routes.
 * Supports real-time message broadcasting.
 */

const express = require('express');
const registry = require('../models/ServiceRegistry');

module.exports = function gatewayHandler(mocks, options = {}) {
    const router = express.Router();
    const { storeRealtimeMessage } = options;

    registry.getAll().forEach(service => {
        service.endpoints.forEach(endpoint => {
            const method = endpoint.method.toLowerCase();
            
            router[method](endpoint.path, (req, res) => {
                console.log(`[Gateway] ${endpoint.method} ${req.originalUrl}`);
                
                try {
                    const mockData = mocks[endpoint.mockDataKey];
                    let rules = null;
                    
                    if (endpoint.behaviorKey) {
                        try {
                            rules = require(`../behaviors/${endpoint.behaviorKey}`);
                        } catch (err) {
                            console.warn(`Behavior not found: ${endpoint.behaviorKey}`);
                        }
                    }
                    
                    let response;
                    if (endpoint.responseTransform) {
                        // The whole mock set is passed as a fourth argument for endpoints that need
                        // to compose several files rather than just their own. Existing transforms
                        // take three parameters and are unaffected.
                        response = endpoint.responseTransform(req, mockData, rules, mocks);
                    } else {
                        response = mockData || { success: true };
                    }
                    
                    // Real-time broadcast for send endpoints. `realtimeType` lets the new
                    // provider-shaped routes label themselves; the old path sniffing stays for
                    // the existing /integrationgateway/* routes.
                    const isSend = endpoint.path.includes('/send')
                        || endpoint.path.includes('/messages')
                        || endpoint.path.includes('/mail');
                    if (storeRealtimeMessage && isSend) {
                        const type = endpoint.realtimeType
                            || (endpoint.path.includes('/sms/') ? 'sms'
                                : endpoint.path.includes('/email/') ? 'email'
                                : 'unknown');
                        storeRealtimeMessage(type, req.body || {}, response);
                    }

                    // A model may answer with a real status code and headers by returning the
                    // envelope below. Anything else keeps the historical behaviour: 200 + JSON.
                    if (response && response.$response === true) {
                        if (response.headers) {
                            Object.entries(response.headers).forEach(([name, value]) => res.set(name, value));
                        }
                        const status = response.status || 200;
                        if (response.body === null || response.body === undefined) {
                            return res.status(status).end();
                        }
                        return res.status(status).json(response.body);
                    }

                    res.json(response);
                } catch (err) {
                    console.error('Gateway handler error:', err);
                    res.status(500).json({
                        status: 'error',
                        message: 'Internal gateway error',
                        code: 'GATEWAY_ERROR'
                    });
                }
            });
            
            console.log(`Registered: ${endpoint.method} ${endpoint.path}`);
        });
    });

    return router;
};
