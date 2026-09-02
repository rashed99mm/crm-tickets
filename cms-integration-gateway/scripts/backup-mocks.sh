# CCE Carbon Mock Server - Backup Script (Bash)
# Creates a backup of all mock data

MOCKS_DIR="$(dirname "$0")/../mocks"
BACKUP_DIR="$MOCKS_DIR/_backups"

mkdir -p "$BACKUP_DIR"

TIMESTAMP=$(date +"%Y-%m-%d-%H-%M-%S")
BACKUP_NAME="backup-$TIMESTAMP"
BACKUP_PATH="$BACKUP_DIR/$BACKUP_NAME"

echo "============================================"
echo "CCE Carbon - Mock Data Backup"
echo "============================================"
echo "Creating backup: $BACKUP_NAME"

# Copy all JSON files
find "$MOCKS_DIR" -name "*.json" -not -path "*_backups*" | while read file; do
    RELATIVE_PATH="${file#$MOCKS_DIR/}"
    DEST_FILE="$BACKUP_PATH/$RELATIVE_PATH"
    DEST_DIR=$(dirname "$DEST_FILE")
    mkdir -p "$DEST_DIR"
    cp "$file" "$DEST_FILE"
done

# Keep only last 10 backups
ls -1td "$BACKUP_DIR"/*/ 2>/dev/null | tail -n +11 | xargs -r rm -rf

echo "Backup created: $BACKUP_PATH"
echo "============================================"
