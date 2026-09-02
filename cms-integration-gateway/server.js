const path = require('path');
const express = require('express');
const jsonServer = require('json-server');
const fs = require('fs');
const cors = require('cors');
const rateLimit = require('express-rate-limit');
const helmet = require('helmet');
const compression = require('compression');
const swaggerUi = require('swagger-ui-express');
const swaggerSpecs = require('./swagger');
const config = require('./config');

// Middleware imports
const gatewayHandler = require('./middlewares/gateway-handler');
const requestLogger = require('./middlewares/request-logger');
const errorHandler = require('./middlewares/error-handler');
const createMockAdminMiddleware = require('./middlewares/mock-admin');

// Global mocks storage
let globalMocks = {};
let jsonServerRouter = null;

const MAX_REALTIME_MESSAGES = 100;
const REALTIME_FILE = path.join(__dirname, 'mocks', '_realtime-messages.json');

/**
 * Load realtime messages from disk
 * @returns {Array} Messages array
 */
function loadRealtimeMessages() {
    try {
        if (fs.existsSync(REALTIME_FILE)) {
            const data = fs.readFileSync(REALTIME_FILE, 'utf8');
            const messages = JSON.parse(data);
            if (Array.isArray(messages)) return messages;
        }
    } catch (err) {
        console.error('[Realtime] Error loading messages:', err.message);
    }
    return [];
}

/**
 * Save realtime messages to disk
 * @param {Array} messages
 */
function saveRealtimeMessages(messages) {
    try {
        const dir = path.dirname(REALTIME_FILE);
        if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
        fs.writeFileSync(REALTIME_FILE, JSON.stringify(messages, null, 2), 'utf8');
    } catch (err) {
        console.error('[Realtime] Error saving messages:', err.message);
    }
}

/**
 * Store a message for real-time viewing
 * @param {string} type - 'sms' or 'email'
 * @param {Object} payload - The request payload
 * @param {Object} response - The server response
 */
function storeRealtimeMessage(type, payload, response) {
    const message = {
        id: `msg-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
        type,
        timestamp: new Date().toISOString(),
        payload: { ...payload },
        response: { ...response }
    };

    const messages = loadRealtimeMessages();
    messages.unshift(message);
    if (messages.length > MAX_REALTIME_MESSAGES) {
        messages.splice(MAX_REALTIME_MESSAGES);
    }
    saveRealtimeMessages(messages);

    console.log(`[Realtime] ${type.toUpperCase()} message stored`);
}

// Export for use in middlewares
module.exports.storeRealtimeMessage = storeRealtimeMessage;

/**
 * Load mock data from JSON files recursively
 */
const loadMockData = () => {
    const mocks = {};
    
    const loadFromDirectory = (dirPath, prefix = '') => {
        if (!fs.existsSync(dirPath)) return;
        
        const items = fs.readdirSync(dirPath, { withFileTypes: true });
        
        for (const item of items) {
            const itemPath = path.join(dirPath, item.name);
            
            if (item.name.startsWith('.') || item.name === '_backups') continue;
            
            if (item.isDirectory()) {
                loadFromDirectory(itemPath, prefix ? `${prefix}-${item.name}` : item.name);
            } else if (item.name.endsWith('.json')) {
                const fileName = item.name.replace('.json', '');
                const serviceName = prefix ? `${prefix}-${fileName}` : fileName;
                
                try {
                    const data = JSON.parse(fs.readFileSync(itemPath, 'utf8'));
                    mocks[serviceName] = data;
                    console.log(`Loaded mock: ${serviceName} (${itemPath})`);
                } catch (err) {
                    console.error(`Error loading ${itemPath}:`, err.message);
                }
            }
        }
    };
    
    loadFromDirectory(config.MOCKS_DIR);
    return mocks;
};

/**
 * Setup security middlewares
 */
const setupSecurity = (server) => {
    server.use(
        helmet.contentSecurityPolicy({
            directives: {
                defaultSrc: ["'self'", "*", "data:", "http:", "https:"],
                scriptSrc: ["'self'", "'unsafe-inline'", "'unsafe-eval'", "*", "data:", "http:", "https:"],
                styleSrc: ["'self'", "'unsafe-inline'", "*", "data:", "http:", "https:"],
                imgSrc: ["'self'", "*", "data:", "http:", "https:"],
                connectSrc: ["'self'", "*", "http:", "https:"],
                formAction: ["'self'", "*"],
                fontSrc: ["'self'", "*", "data:", "http:", "https:"],
                objectSrc: ["'none'"],
                mediaSrc: ["'self'", "*"],
                frameSrc: ["'self'", "*"]
            }
        })
    );
    
    server.use(cors({
        origin: '*',
        methods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS'],
        allowedHeaders: ['Origin', 'X-Requested-With', 'Content-Type', 'Accept', 'Authorization']
    }));
    
    server.use(rateLimit(config.RATE_LIMIT));
};

/**
 * Setup and configure the server
 */
const setupServer = () => {
    const server = express();
    const mocks = loadMockData();
    globalMocks = mocks;
    
    // Create json-server router for automatic REST endpoints
    const createJsonServerRouter = (mocksData) => {
        const router = jsonServer.router(mocksData);
        router.render = (req, res) => {
            console.log(`[JSON Server] ${req.method} ${req.originalUrl}`);
            res.jsonp(res.locals.data);
        };
        return router;
    };
    
    jsonServerRouter = createJsonServerRouter(mocks);
    
    // Hot-reload callback
    const onMocksReload = (newMocks, changedService) => {
        console.log(`[Mock Admin] Mocks updated${changedService ? ` (${changedService})` : ''}`);
        jsonServerRouter = createJsonServerRouter(newMocks);
    };
    
    // Security
    setupSecurity(server);
    server.use(compression());
    
    // Swagger
    server.use('/api-docs', swaggerUi.serve);
    server.get('/api-docs', swaggerUi.setup(swaggerSpecs, {
        explorer: true,
        customCss: '.swagger-ui .topbar { display: none }',
        customSiteTitle: 'CommandCenter CMS - Integration Gateway Mocks'
    }));
    
    server.get('/swagger.json', (req, res) => {
        res.setHeader('Access-Control-Allow-Origin', '*');
        res.json(swaggerSpecs);
    });
    
    // Static files
    server.use(express.static(path.join(__dirname, 'public')));
    server.get('/', (req, res) => res.sendFile(path.join(__dirname, 'public', 'index.html')));
    server.get('/mock-manager', (req, res) => res.sendFile(path.join(__dirname, 'public', 'mock-manager.html')));
    server.get('/realtime', (req, res) => res.sendFile(path.join(__dirname, 'public', 'realtime-viewer.html')));
    
    // Body parsers
    server.use(express.json({ limit: '10mb' }));
    server.use(express.urlencoded({ extended: true, limit: '10mb' }));
    
    // Request logger
    server.use(requestLogger());
    
    // Mock Admin API
    server.use('/api/mock-admin', createMockAdminMiddleware(globalMocks, {
        mocksDir: config.MOCKS_DIR,
        onReload: onMocksReload,
        autoBackup: true
    }));
    
    // Server info
    server.get('/api/server-info', (req, res) => {
        res.json({
            name: 'CommandCenter CMS - Integration Gateway Mocks',
            version: '1.0.0',
            company: 'CommandCenter CMS',
            tagline: 'Customer Management System Integration Gateway',
            environment: config.NODE_ENV,
            port: config.PORT,
            timestamp: new Date().toISOString(),
            uptime: process.uptime(),
            services: ['erp-gateway', 'sms-gateway', 'email-gateway', 'whatsapp-gateway', 'auth-gateway']
        });
    });
    
    // Health check
    server.get('/health', (req, res) => {
        res.json({
            status: 'healthy',
            timestamp: new Date().toISOString(),
            uptime: process.uptime(),
            memory: process.memoryUsage()
        });
    });

    // Real-time API - Get recent messages (file-based, works on any hosting)
    server.get('/api/realtime/messages', (req, res) => {
        const { type, limit = 50 } = req.query;
        let messages = loadRealtimeMessages();
        
        if (type) {
            messages = messages.filter(m => m.type === type);
        }
        
        const limitNum = parseInt(limit, 10);
        messages = messages.slice(0, limitNum);
        
        res.json({
            success: true,
            data: {
                messages,
                total: messages.length
            }
        });
    });

    // Real-time API - SSE Stream (not reliable on free hosting, kept for backward compat)
    server.get('/api/realtime/stream', (req, res) => {
        res.setHeader('Content-Type', 'text/event-stream');
        res.setHeader('Cache-Control', 'no-cache');
        res.setHeader('Connection', 'keep-alive');
        res.setHeader('Access-Control-Allow-Origin', '*');
        
        console.log('[Realtime] SSE client connected (may not work on all hosts)');
        
        // Send connection message
        res.write(`data: ${JSON.stringify({ type: 'connected', timestamp: new Date().toISOString() })}\n\n`);
        
        // Send existing messages
        const messages = loadRealtimeMessages();
        if (messages.length > 0) {
            res.write(`data: ${JSON.stringify({ type: 'history', messages: messages.slice(0, 20) })}\n\n`);
        }
        
        // Keep connection alive with heartbeat every 15 seconds
        const heartbeat = setInterval(() => {
            try {
                res.write(':heartbeat\n\n');
            } catch (err) {
                clearInterval(heartbeat);
            }
        }, 15000);
        
        // Remove client on disconnect
        req.on('close', () => {
            clearInterval(heartbeat);
            console.log('[Realtime] SSE client disconnected');
        });
    });
    
    // Gateway handler (custom middleware for SMS/Email)
    server.use(gatewayHandler(globalMocks, { storeRealtimeMessage }));
    
    // JSON Server rewrites and router
    const routes = require(config.ROUTES_FILE);
    server.use(jsonServer.rewriter(routes));
    server.use((req, res, next) => {
        if (jsonServerRouter) {
            return jsonServerRouter(req, res, next);
        }
        next();
    });
    
    // Error handler (last)
    server.use(errorHandler);
    
    return server;
};

/**
 * Start server
 */
const startServer = () => {
    try {
        const app = setupServer();
        
        app.listen(config.PORT, () => {
            console.log(`=================================================`);
            console.log(` CommandCenter CMS - Integration Gateway Mock Server`);
            console.log(` Customer Management System Integration Gateway`);
            console.log(` Running on port ${config.PORT}`);
            console.log(` Swagger Docs:    http://localhost:${config.PORT}/api-docs`);
            console.log(` Mock Manager:    http://localhost:${config.PORT}/mock-manager`);
            console.log(` Realtime Viewer: http://localhost:${config.PORT}/realtime`);
            console.log(`=================================================`);
        });
        
        // Graceful shutdown
        process.on('SIGTERM', () => process.exit(0));
        process.on('SIGINT', () => process.exit(0));
        
    } catch (error) {
        console.error('Failed to start server:', error);
        process.exit(1);
    }
};

startServer();
