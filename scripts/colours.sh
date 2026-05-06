# More colours in top answer on
# - https://stackoverflow.com/questions/5947742/how-to-change-the-output-color-of-echo-in-linux
# Add colours to echo console statements.
# Ensure you reset console colours after using them with ${NC}
# > echo ${BLUE}Something is happening...${NC}
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
PURPLE='\033[0;35m'
CYAN='\033[0;36m'
NC='\033[0m' # No Colour
