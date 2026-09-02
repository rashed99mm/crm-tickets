/**
 * Mock Admin Middleware
 * Provides CRUD operations for dynamic mock management.
 */

const express = require('express');
const fs = require('fs');
const path = require('path');

function writeFileAtomic(filePath, data) {
    const dir = path.dirname(filePath);
    if (!fs.existsSync(dir)) {
        fs.mkdirSync(dir, { recursive: true });
    }
    const tempPath = `${filePath}.tmp`;
    fs.writeFileSync(tempPath, JSON.stringify(data, null, 2), 'utf8');
    fs.renameSync(tempPath, filePath);
}

function createBackup(mocksDir) {
    const backupDir = path.join(mocksDir, '_backups');
    if (!fs.existsSync(backupDir)) {
        fs.mkdirSync(backupDir, { recursive: true });
    }
    
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
    const backupName = `backup-${timestamp}`;
    const backupPath = path.join(backupDir, backupName);
    fs.mkdirSync(backupPath, { recursive: true });
    
    // Copy all JSON files
    function copyDir(src, dest) {
        const entries = fs.readdirSync(src, { withFileTypes: true });
        for (const entry of entries) {
            const srcPath = path.join(src, entry.name);
            const destPath = path.join(dest, entry.name);
            if (entry.isDirectory() && entry.name !== '_backups') {
                fs.mkdirSync(destPath, { recursive: true });
                copyDir(srcPath, destPath);
            } else if (entry.name.endsWith('.json')) {
                fs.copyFileSync(srcPath, destPath);
            }
        }
    }
    
    copyDir(mocksDir, backupPath);
    console.log(`Backup created: ${backupPath}`);
    
    // Keep only last 10 backups
    const backups = fs.readdirSync(backupDir)
        .map(name => ({ name, path: path.join(backupDir, name) }))
        .sort((a, b) => fs.statSync(b.path).mtime - fs.statSync(a.path).mtime);
    
    if (backups.length > 10) {
        backups.slice(10).forEach(b => {
            fs.rmSync(b.path, { recursive: true, force: true });
        });
    }
    
    return { backupName, path: backupPath };
}

module.exports = function createMockAdminMiddleware(globalMocks, options = {}) {
    const {
        mocksDir = path.join(__dirname, '../../mocks'),
        onReload = null,
        autoBackup = true
    } = options;

    const router = express.Router();
    router.use(express.json({ limit: '10mb' }));

    const reloadService = (serviceName, data) => {
        globalMocks[serviceName] = data;
        if (onReload) onReload(globalMocks, serviceName);
    };

    const removeService = (serviceName) => {
        delete globalMocks[serviceName];
        if (onReload) onReload(globalMocks, null);
    };

    const saveServiceToDisk = (name, data) => {
        const parts = name.split('-');
        // Determine group: if first part matches a subdirectory, use it
        const groupDir = parts.length > 1 ? parts[0] : null;
        const fileName = groupDir ? parts.slice(1).join('-') : name;
        
        const filePath = groupDir
            ? path.join(mocksDir, groupDir, `${fileName}.json`)
            : path.join(mocksDir, `${fileName}.json`);
            
        writeFileAtomic(filePath, data);
        return filePath;
    };

    // GET /services - List all services
    router.get('/services', (req, res) => {
        try {
            const services = Object.keys(globalMocks).map(name => {
                const data = globalMocks[name];
                let recordCount = 0;
                
                if (Array.isArray(data)) {
                    recordCount = data.length;
                } else if (typeof data === 'object' && data !== null) {
                    for (const key of Object.keys(data)) {
                        if (Array.isArray(data[key])) recordCount += data[key].length;
                    }
                }
                
                const parts = name.split('-');
                const group = parts.length > 1 ? parts[0] : 'root';
                
                return {
                    name,
                    group,
                    displayName: name.replace(/[-_]/g, ' ').replace(/\b\w/g, l => l.toUpperCase()),
                    recordCount,
                    type: Array.isArray(data) ? 'array' : typeof data
                };
            });

            const grouped = services.reduce((acc, s) => {
                if (!acc[s.group]) acc[s.group] = [];
                acc[s.group].push(s);
                return acc;
            }, {});

            res.json({ success: true, data: { services, grouped, total: services.length } });
        } catch (err) {
            res.status(500).json({ success: false, error: { code: 'LIST_ERROR', message: err.message } });
        }
    });

    // GET /services/:name
    router.get('/services/:name', (req, res) => {
        try {
            const { name } = req.params;
            if (!globalMocks.hasOwnProperty(name)) {
                return res.status(404).json({ success: false, error: { code: 'NOT_FOUND', message: `Service "${name}" not found` } });
            }
            
            res.json({
                success: true,
                data: {
                    name,
                    content: globalMocks[name],
                    type: Array.isArray(globalMocks[name]) ? 'array' : typeof globalMocks[name]
                }
            });
        } catch (err) {
            res.status(500).json({ success: false, error: { code: 'GET_ERROR', message: err.message } });
        }
    });

    // PUT /services/:name
    router.put('/services/:name', (req, res) => {
        try {
            const { name } = req.params;
            const { data } = req.body;
            
            if (!globalMocks.hasOwnProperty(name)) {
                return res.status(404).json({ success: false, error: { code: 'NOT_FOUND', message: `Service "${name}" not found` } });
            }
            
            if (autoBackup) createBackup(mocksDir);
            
            saveServiceToDisk(name, data);
            reloadService(name, data);
            
            res.json({ success: true, message: `Service "${name}" updated successfully` });
        } catch (err) {
            res.status(400).json({ success: false, error: { code: 'UPDATE_ERROR', message: err.message } });
        }
    });

    // POST /services/:name/records
    router.post('/services/:name/records', (req, res) => {
        try {
            const { name } = req.params;
            const { record } = req.body;
            
            if (!globalMocks.hasOwnProperty(name)) {
                return res.status(404).json({ success: false, error: { code: 'NOT_FOUND', message: `Service "${name}" not found` } });
            }
            
            const data = globalMocks[name];
            let targetArray = null;
            
            if (Array.isArray(data)) {
                targetArray = data;
            } else {
                for (const key of Object.keys(data)) {
                    if (Array.isArray(data[key])) {
                        targetArray = data[key];
                        break;
                    }
                }
            }
            
            if (!targetArray) {
                return res.status(400).json({ success: false, error: { code: 'NOT_ARRAY', message: 'Service data is not array-based' } });
            }
            
            targetArray.push(record);
            
            if (autoBackup) createBackup(mocksDir);
            saveServiceToDisk(name, data);
            if (onReload) onReload(globalMocks, name);
            
            res.status(201).json({ success: true, data: { record, index: targetArray.length - 1 } });
        } catch (err) {
            res.status(400).json({ success: false, error: { code: 'ADD_ERROR', message: err.message } });
        }
    });

    // PUT /services/:name/records/:index
    router.put('/services/:name/records/:index', (req, res) => {
        try {
            const { name, index } = req.params;
            const { record } = req.body;
            const idx = parseInt(index, 10);
            
            if (!globalMocks.hasOwnProperty(name)) {
                return res.status(404).json({ success: false, error: { code: 'NOT_FOUND', message: `Service "${name}" not found` } });
            }
            
            const data = globalMocks[name];
            let targetArray = null;
            
            if (Array.isArray(data)) {
                targetArray = data;
            } else {
                for (const key of Object.keys(data)) {
                    if (Array.isArray(data[key])) {
                        targetArray = data[key];
                        break;
                    }
                }
            }
            
            if (!targetArray || idx < 0 || idx >= targetArray.length) {
                return res.status(404).json({ success: false, error: { code: 'NOT_FOUND', message: 'Record not found' } });
            }
            
            targetArray[idx] = record;
            
            if (autoBackup) createBackup(mocksDir);
            saveServiceToDisk(name, data);
            if (onReload) onReload(globalMocks, name);
            
            res.json({ success: true, message: 'Record updated successfully' });
        } catch (err) {
            res.status(400).json({ success: false, error: { code: 'UPDATE_ERROR', message: err.message } });
        }
    });

    // DELETE /services/:name/records/:index
    router.delete('/services/:name/records/:index', (req, res) => {
        try {
            const { name, index } = req.params;
            const idx = parseInt(index, 10);
            
            if (!globalMocks.hasOwnProperty(name)) {
                return res.status(404).json({ success: false, error: { code: 'NOT_FOUND', message: `Service "${name}" not found` } });
            }
            
            const data = globalMocks[name];
            let targetArray = null;
            
            if (Array.isArray(data)) {
                targetArray = data;
            } else {
                for (const key of Object.keys(data)) {
                    if (Array.isArray(data[key])) {
                        targetArray = data[key];
                        break;
                    }
                }
            }
            
            if (!targetArray || idx < 0 || idx >= targetArray.length) {
                return res.status(404).json({ success: false, error: { code: 'NOT_FOUND', message: 'Record not found' } });
            }
            
            const removed = targetArray.splice(idx, 1)[0];
            
            if (autoBackup) createBackup(mocksDir);
            saveServiceToDisk(name, data);
            if (onReload) onReload(globalMocks, name);
            
            res.json({ success: true, data: { removed } });
        } catch (err) {
            res.status(500).json({ success: false, error: { code: 'DELETE_ERROR', message: err.message } });
        }
    });

    // POST /reload
    router.post('/reload', (req, res) => {
        try {
            if (onReload) onReload(globalMocks, null);
            res.json({ success: true, message: 'Mocks reloaded' });
        } catch (err) {
            res.status(500).json({ success: false, error: { code: 'RELOAD_ERROR', message: err.message } });
        }
    });

    // POST /backup
    router.post('/backup', (req, res) => {
        try {
            const result = createBackup(mocksDir);
            res.json({ success: true, data: result });
        } catch (err) {
            res.status(500).json({ success: false, error: { code: 'BACKUP_ERROR', message: err.message } });
        }
    });

    return router;
};
