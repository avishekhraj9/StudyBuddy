// Global State
let token = localStorage.getItem('study_buddy_token') || '';
let currentUser = null;
let currentNotes = [];
let currentQuiz = null;
let currentQuizIndex = 0;
let currentQuizScore = 0;
let activeQuizAnswers = []; // tracking taken options
let currentFlashcards = [];
let currentFlashcardIndex = 0;
let activeChatNoteId = null;

const API_BASE = '/api';

// Initialize App
document.addEventListener('DOMContentLoaded', () => {
    setupAuthListeners();
    setupSidebarNavigation();
    setupNoteUpload();
    setupSearchFilters();
    setupQuizListeners();
    setupPlannerListeners();
    setupRoadmapListeners();
    setupMobileMenu();
    
    if (token) {
        verifySession();
    } else {
        showAuthScreen();
    }
});

// ==================== SESSION & AUTH MANAGEMENT ====================

function showAuthScreen() {
    document.getElementById('authContainer').style.display = 'flex';
    document.getElementById('appContainer').style.display = 'none';
}

function showAppScreen() {
    document.getElementById('authContainer').style.display = 'none';
    document.getElementById('appContainer').style.display = 'grid';
    switchView('dashboard');
    loadProfileStats();
}

function verifySession() {
    // Attempt to load profile to check token validity
    fetchWithAuth('/auth/profile')
        .then(res => {
            if (!res.ok) throw new Error('Session expired');
            return res.json();
        })
        .then(data => {
            currentUser = data.user;
            updateUserProfileUI(data);
            showAppScreen();
        })
        .catch(() => {
            logout();
        });
}

function setupAuthListeners() {
    const toRegister = document.getElementById('toRegister');
    const toLogin = document.getElementById('toLogin');
    const loginCard = document.getElementById('loginCard');
    const registerCard = document.getElementById('registerCard');

    toRegister.addEventListener('click', (e) => {
        e.preventDefault();
        loginCard.style.display = 'none';
        registerCard.style.display = 'block';
    });

    toLogin.addEventListener('click', (e) => {
        e.preventDefault();
        registerCard.style.display = 'none';
        loginCard.style.display = 'block';
    });

    // Login Form Submit
    document.getElementById('loginForm').addEventListener('submit', (e) => {
        e.preventDefault();
        const email = document.getElementById('loginEmail').value;
        const password = document.getElementById('loginPassword').value;

        fetch(`${API_BASE}/auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password })
        })
        .then(async res => {
            const data = await res.json();
            if (!res.ok) throw new Error(data.message || 'Login failed');
            return data;
        })
        .then(data => {
            token = data.token;
            localStorage.setItem('study_buddy_token', token);
            currentUser = data.user;
            verifySession();
        })
        .catch(err => {
            alert(err.message);
        });
    });

    // Register Form Submit
    document.getElementById('registerForm').addEventListener('submit', (e) => {
        e.preventDefault();
        const name = document.getElementById('registerName').value;
        const email = document.getElementById('registerEmail').value;
        const password = document.getElementById('registerPassword').value;

        fetch(`${API_BASE}/auth/register`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name, email, password })
        })
        .then(async res => {
            const data = await res.json();
            if (!res.ok) throw new Error(data.message || 'Registration failed');
            return data;
        })
        .then(() => {
            alert('Registration successful! Please log in.');
            registerCard.style.display = 'none';
            loginCard.style.display = 'block';
        })
        .catch(err => {
            alert(err.message);
        });
    });

    // Logout Button
    document.getElementById('btnLogout').addEventListener('click', logout);
}

function logout() {
    token = '';
    localStorage.removeItem('study_buddy_token');
    currentUser = null;
    showAuthScreen();
}

function updateUserProfileUI(data) {
    const name = data.user.name;
    const email = data.user.email;
    const initial = name ? name.charAt(0).toUpperCase() : 'U';

    document.getElementById('sidebarAvatar').innerText = initial;
    document.getElementById('sidebarUserName').innerText = name;
    document.getElementById('sidebarUserEmail').innerText = email;
    document.getElementById('dashGreetingName').innerText = name;
    
    // Settings profile
    document.getElementById('settingsUserId').value = `User #${data.user.id}`;
    document.getElementById('settingsUserJoined').value = new Date(data.user.createdAt).toLocaleDateString();
}

// ==================== AUTHENTICATED REQUESTS HELPER ====================

function fetchWithAuth(url, options = {}) {
    const headers = options.headers || {};
    headers['Authorization'] = `Bearer ${token}`;
    
    const apiKey = localStorage.getItem('study_buddy_api_key');
    if (apiKey) {
        headers['X-OpenAI-Key'] = apiKey;
    }
    
    options.headers = headers;
    return fetch(`${API_BASE}${url}`, options);
}

// ==================== SIDEBAR ROUTING ====================

function setupSidebarNavigation() {
    const navItems = document.querySelectorAll('.nav-item');
    navItems.forEach(item => {
        item.addEventListener('click', (e) => {
            e.preventDefault();
            const viewName = item.getAttribute('data-view');
            switchView(viewName);
        });
    });
}

function switchView(viewName) {
    // Hide all views
    document.querySelectorAll('.app-view').forEach(view => {
        view.style.display = 'none';
    });

    // Remove active class from menu items
    document.querySelectorAll('.nav-item').forEach(item => {
        item.classList.remove('active');
    });
    document.querySelectorAll('.mobile-nav-item').forEach(item => {
        item.classList.remove('active');
    });

    // Show selected view
    const activeView = document.getElementById(`view-${viewName}`);
    if (activeView) {
        activeView.style.display = 'block';
    }

    // Set menu item active
    const activeMenu = document.querySelector(`.nav-item[data-view="${viewName}"]`);
    if (activeMenu) {
        activeMenu.classList.add('active');
    }
    const activeMobileMenu = document.querySelector(`.mobile-nav-bar [data-view="${viewName}"]`);
    if (activeMobileMenu) {
        activeMobileMenu.classList.add('active');
    }

    // View-specific loaders
    if (viewName === 'dashboard') {
        loadProfileStats();
    } else if (viewName === 'notes') {
        loadNotes();
        loadSubjects();
    } else if (viewName === 'chat') {
        loadChatDocuments();
    } else if (viewName === 'quizzes') {
        loadQuizDocuments();
    } else if (viewName === 'flashcards') {
        loadFlashcardDocuments();
    } else if (viewName === 'planner') {
        loadPlannerTasks();
        loadExamCountdown();
    } else if (viewName === 'roadmap') {
        loadRoadmap();
        loadRecommendations();
    } else if (viewName === 'settings') {
        loadSettings();
    }
}

// ==================== DASHBOARD & STATS ====================

function loadProfileStats() {
    fetchWithAuth('/auth/profile')
        .then(res => res.json())
        .then(data => {
            document.getElementById('statNotes').innerText = data.stats.notesCount;
            document.getElementById('statQuizzes').innerText = data.stats.quizzesCompleted;
            document.getElementById('statScore').innerText = data.stats.averageScore;
            document.getElementById('statStreak').innerText = data.stats.streak;

            // Compute and render simulated XP on Leaderboard comparison
            // Rule: 100 XP per Note upload + 150 XP per Quiz completed + score * 2
            const xp = (data.stats.notesCount * 100) + (data.stats.quizzesCompleted * 150) + (data.stats.averageScore * 10);
            document.getElementById('leaderboardUserScore').innerText = `${xp.toLocaleString()} XP`;
            document.getElementById('leaderboardUserName').innerText = `${currentUser.name} (You)`;
        })
        .catch(err => console.error('Error loading stats:', err));
}

// ==================== STUDY MATERIALS & UPLOADS ====================

function setupNoteUpload() {
    const uploadZone = document.getElementById('uploadZone');
    const fileInput = document.getElementById('fileInput');

    uploadZone.addEventListener('click', () => fileInput.click());

    uploadZone.addEventListener('dragover', (e) => {
        e.preventDefault();
        uploadZone.style.borderColor = 'var(--primary)';
        uploadZone.style.background = 'rgba(99, 102, 241, 0.05)';
    });

    uploadZone.addEventListener('dragleave', () => {
        uploadZone.style.borderColor = 'var(--border-color)';
        uploadZone.style.background = 'rgba(255, 255, 255, 0.02)';
    });

    uploadZone.addEventListener('drop', (e) => {
        e.preventDefault();
        uploadZone.style.borderColor = 'var(--border-color)';
        uploadZone.style.background = 'rgba(255, 255, 255, 0.02)';
        
        if (e.dataTransfer.files.length > 0) {
            handleFileUpload(e.dataTransfer.files[0]);
        }
    });

    fileInput.addEventListener('change', () => {
        if (fileInput.files.length > 0) {
            handleFileUpload(fileInput.files[0]);
            fileInput.value = ''; // clear
        }
    });
}

function handleFileUpload(file) {
    const progressContainer = document.getElementById('uploadProgressContainer');
    const progressFill = document.getElementById('uploadProgressFill');
    const progressPercent = document.getElementById('uploadProgressPercent');
    const statusText = document.getElementById('uploadStatusText');
    const subjectInput = document.getElementById('uploadSubject');

    progressContainer.style.display = 'block';
    progressFill.style.width = '10%';
    progressPercent.innerText = '10%';
    statusText.innerText = 'Uploading file...';

    const formData = new FormData();
    formData.append('file', file);
    formData.append('subject', subjectInput.value || 'General');

    // Simulate progress increments for UI responsiveness
    let progress = 10;
    const interval = setInterval(() => {
        if (progress < 85) {
            progress += 5;
            progressFill.style.width = `${progress}%`;
            progressPercent.innerText = `${progress}%`;
            if (progress === 40) statusText.innerText = 'Extracting note contents...';
            if (progress === 70) statusText.innerText = 'AI summarizing study concepts...';
        }
    }, 400);

    fetchWithAuth('/notes/upload', {
        method: 'POST',
        body: formData
    })
    .then(async res => {
        clearInterval(interval);
        const data = await res.json();
        if (!res.ok) throw new Error(data.message || 'Upload failed');
        return data;
    })
    .then(() => {
        progressFill.style.width = '100%';
        progressPercent.innerText = '100%';
        statusText.innerText = 'Complete!';
        
        setTimeout(() => {
            progressContainer.style.display = 'none';
            subjectInput.value = '';
            loadNotes();
            loadSubjects();
        }, 1000);
    })
    .catch(err => {
        clearInterval(interval);
        progressContainer.style.display = 'none';
        alert(err.message);
    });
}

function setupSearchFilters() {
    const search = document.getElementById('noteSearchInput');
    const filter = document.getElementById('noteSubjectFilter');

    search.addEventListener('input', debounce(() => loadNotes(), 300));
    filter.addEventListener('change', () => loadNotes());
}

function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

function loadSubjects() {
    fetchWithAuth('/notes/subjects')
        .then(res => res.json())
        .then(subjects => {
            const filter = document.getElementById('noteSubjectFilter');
            // Keep first option
            filter.innerHTML = '<option value="">All Subjects</option>';
            subjects.forEach(subject => {
                const opt = document.createElement('option');
                opt.value = subject;
                opt.innerText = subject;
                filter.appendChild(opt);
            });
        })
        .catch(err => console.error(err));
}

function loadNotes() {
    const search = document.getElementById('noteSearchInput').value;
    const subject = document.getElementById('noteSubjectFilter').value;
    const grid = document.getElementById('notesGrid');

    grid.innerHTML = '<div style="grid-column: 1/-1; text-align: center; color: var(--text-muted);">Loading study documents...</div>';

    let url = `/notes?search=${encodeURIComponent(search)}`;
    if (subject) {
        url += `&subject=${encodeURIComponent(subject)}`;
    }

    fetchWithAuth(url)
        .then(res => res.json())
        .then(notes => {
            currentNotes = notes;
            if (notes.length === 0) {
                grid.innerHTML = '<div style="grid-column: 1/-1; text-align: center; color: var(--text-muted); padding: 40px;">No notes found. Upload some study guides above!</div>';
                return;
            }

            grid.innerHTML = '';
            notes.forEach(note => {
                const card = document.createElement('div');
                card.className = 'note-card';
                card.innerHTML = `
                    <div class="note-card-header">
                        <span class="note-tag">${escapeHtml(note.subject)}</span>
                        <button class="note-delete-btn" onclick="deleteNote(${note.id}, event)" title="Delete note">
                            <i class="fa-solid fa-trash-can"></i>
                        </button>
                    </div>
                    <h3 class="note-title">${escapeHtml(note.title)}</h3>
                    <div class="note-meta">
                        <i class="fa-regular fa-calendar"></i>
                        <span>${new Date(note.uploadDate).toLocaleDateString()}</span>
                    </div>
                    <div class="note-card-actions">
                        <button class="note-action-btn" onclick="viewSummary(${note.id})">
                            <i class="fa-solid fa-book-open"></i>
                            <span>Summary</span>
                        </button>
                        <button class="note-action-btn" onclick="startChatFromNote(${note.id})">
                            <i class="fa-solid fa-comments"></i>
                            <span>Chat</span>
                        </button>
                        <button class="note-action-btn" onclick="startQuizFromNote(${note.id})">
                            <i class="fa-solid fa-graduation-cap"></i>
                            <span>Quiz</span>
                        </button>
                    </div>
                `;
                grid.appendChild(card);
            });
        })
        .catch(err => {
            grid.innerHTML = `<div style="grid-column: 1/-1; text-align: center; color: var(--danger);">Error loading materials: ${err.message}</div>`;
        });
}

function deleteNote(id, event) {
    event.stopPropagation();
    if (!confirm('Are you sure you want to delete this study note and all its quizzes/chats?')) return;

    fetchWithAuth(`/notes/${id}`, { method: 'DELETE' })
        .then(res => res.json())
        .then(() => {
            loadNotes();
            loadSubjects();
        })
        .catch(err => alert(err.message));
}

// ==================== NOTES MODAL SUMMARY ====================

let activeModalNoteId = null;

function viewSummary(noteId) {
    activeModalNoteId = noteId;
    const modal = document.getElementById('noteModal');
    const title = document.getElementById('noteModalTitle');
    const summaryArea = document.getElementById('noteModalSummary');
    
    title.innerText = 'Loading Summary...';
    summaryArea.innerHTML = '<p style="text-align:center;">Fetching AI summarization...</p>';
    modal.style.display = 'flex';
    
    // Reset to summary tab and load recommendations
    switchModalTab('summary');
    loadRecommendationsForNote(noteId);

    fetchWithAuth(`/notes/${noteId}`)
        .then(res => res.json())
        .then(note => {
            title.innerText = note.title;
            summaryArea.innerHTML = parseMarkdown(note.summary || 'Summary is generating or empty. Try regenerating.');
            
            // Re-bind actions
            document.getElementById('btnModalRegenSummary').onclick = () => regenerateSummary(noteId);
            document.getElementById('btnModalOpenChat').onclick = () => {
                closeNoteModal();
                startChatFromNote(noteId);
            };
        })
        .catch(err => {
            summaryArea.innerHTML = `<p style="color:var(--danger)">Error loading note: ${err.message}</p>`;
        });
}

function regenerateSummary(noteId) {
    const summaryArea = document.getElementById('noteModalSummary');
    summaryArea.innerHTML = '<p style="text-align:center;color:var(--primary);">AI is rewriting study materials summary...</p>';

    fetchWithAuth(`/notes/${noteId}/summary`, { method: 'POST' })
        .then(res => res.json())
        .then(data => {
            summaryArea.innerHTML = parseMarkdown(data.summary);
        })
        .catch(err => {
            summaryArea.innerHTML = `<p style="color:var(--danger)">Error generating summary: ${err.message}</p>`;
        });
}

function closeNoteModal() {
    document.getElementById('noteModal').style.display = 'none';
    activeModalNoteId = null;
}

// Close modal if clicking outside
window.onclick = (e) => {
    const modal = document.getElementById('noteModal');
    const youtubeModal = document.getElementById('youtubeModal');
    if (e.target === modal) {
        closeNoteModal();
    }
    if (e.target === youtubeModal) {
        closeYoutubeModal();
    }
};

// ==================== YOUTUBE & RESOURCE HUB INTEGRATION ====================

function switchModalTab(tabName) {
    const tabs = ['summary', 'videos', 'resources'];
    tabs.forEach(t => {
        const tabEl = document.getElementById(`modalTab-${t}`);
        const panelEl = document.getElementById(`modalPanel-${t}`);
        
        if (t === tabName) {
            tabEl.classList.add('active');
            tabEl.style.color = 'var(--text-main)';
            tabEl.style.borderBottomColor = 'var(--primary)';
            panelEl.style.display = 'block';
        } else {
            tabEl.classList.remove('active');
            tabEl.style.color = 'var(--text-muted)';
            tabEl.style.borderBottomColor = 'transparent';
            panelEl.style.display = 'none';
        }
    });
}

function loadRecommendationsForNote(noteId) {
    const videoGrid = document.getElementById('modalVideoGrid');
    const docsList = document.getElementById('modalDocsList');
    const practiceList = document.getElementById('modalPracticeList');
    const projectList = document.getElementById('modalProjectList');

    videoGrid.innerHTML = '<div style="grid-column: 1/-1; text-align: center; color: var(--text-muted); padding: 20px;"><i class="fa-solid fa-spinner fa-spin"></i> Fetching recommended tutorials...</div>';
    docsList.innerHTML = '<p style="text-align: center; color: var(--text-muted); padding: 10px;"><i class="fa-solid fa-spinner fa-spin"></i> Loading articles...</p>';
    practiceList.innerHTML = '<p style="text-align: center; color: var(--text-muted); padding: 10px;"><i class="fa-solid fa-spinner fa-spin"></i> Loading challenges...</p>';
    projectList.innerHTML = '<p style="text-align: center; color: var(--text-muted); padding: 10px;"><i class="fa-solid fa-spinner fa-spin"></i> Loading projects...</p>';

    Promise.all([
        fetchWithAuth(`/resources/note/${noteId}`).then(res => {
            if (!res.ok) throw new Error('Failed to load resources');
            return res.json();
        }),
        fetchWithAuth('/resources/progress').then(res => {
            if (!res.ok) throw new Error('Failed to load progress');
            return res.json();
        })
    ])
    .then(([resources, completedIds]) => {
        const completedSet = new Set(completedIds);
        renderResources(resources, completedSet);
    })
    .catch(err => {
        console.error('Error loading note resources:', err);
        videoGrid.innerHTML = '<div style="grid-column: 1/-1; text-align: center; color: var(--danger);">Failed to load recommendations.</div>';
        docsList.innerHTML = '<p style="color: var(--danger); text-align: center;">Error loading resources.</p>';
    });
}

function renderResources(resources, completedSet) {
    const videoGrid = document.getElementById('modalVideoGrid');
    const docsList = document.getElementById('modalDocsList');
    const practiceList = document.getElementById('modalPracticeList');
    const projectList = document.getElementById('modalProjectList');

    videoGrid.innerHTML = '';
    docsList.innerHTML = '';
    practiceList.innerHTML = '';
    projectList.innerHTML = '';

    let videosCount = 0;
    let docsCount = 0;
    let practiceCount = 0;
    let projectCount = 0;

    resources.forEach(res => {
        const isCompleted = completedSet.has(res.resourceId);
        const completeClass = isCompleted ? 'checked' : '';
        const checkIcon = isCompleted ? 'fa-solid fa-square-check' : 'fa-regular fa-square';
        
        if (res.type === 'Video') {
            videosCount++;
            const thumbnail = `https://img.youtube.com/vi/${res.resourceId}/mqdefault.jpg`;
            const card = document.createElement('div');
            card.className = 'video-card';
            card.innerHTML = `
                <div class="video-thumbnail-container" onclick="playYoutubeVideo('${res.resourceId}', '${escapeHtml(res.title)}')">
                    <img class="video-thumbnail" src="${thumbnail}" alt="${escapeHtml(res.title)}" loading="lazy">
                    <div class="video-play-overlay">
                        <div class="video-play-btn"><i class="fa-solid fa-play"></i></div>
                    </div>
                    <span class="video-duration">${escapeHtml(res.durationOrDetails || 'Video')}</span>
                </div>
                <div class="video-info">
                    <div class="video-title" onclick="playYoutubeVideo('${res.resourceId}', '${escapeHtml(res.title)}')" title="${escapeHtml(res.title)}">${escapeHtml(res.title)}</div>
                    <div class="video-channel">
                        <i class="fa-solid fa-circle-check" style="color: var(--primary); font-size: 10px;"></i>
                        <span>${escapeHtml(res.channelOrPublisher)}</span>
                    </div>
                    <div style="display: flex; justify-content: space-between; align-items: center; margin-top: auto; padding-top: 8px;">
                        <div class="video-rating">
                            <i class="fa-solid fa-star"></i>
                            <span>${res.rating.toFixed(1)}</span>
                        </div>
                        <button class="resource-check-btn ${completeClass}" data-resource-id="${res.resourceId}" onclick="toggleResourceProgress('${res.resourceId}', event)" style="background: none; border: none; color: ${isCompleted ? 'var(--success)' : 'var(--text-muted)'}; cursor: pointer; font-size: 16px; padding: 4px;" title="${isCompleted ? 'Marked Completed' : 'Mark Completed'}">
                            <i class="${checkIcon}"></i>
                        </button>
                    </div>
                </div>
            `;
            videoGrid.appendChild(card);
        } else {
            const isLink = res.resourceId.startsWith('http');
            const targetUrl = isLink ? res.resourceId : '#';
            const item = document.createElement('div');
            item.className = 'resource-item';
            item.innerHTML = `
                <div class="resource-item-details">
                    <div class="resource-checkbox ${completeClass}" data-resource-id="${res.resourceId}" onclick="toggleResourceProgress('${res.resourceId}', event)" title="${isCompleted ? 'Completed' : 'Mark Completed'}">
                        <i class="fa-solid fa-check"></i>
                    </div>
                    <div class="resource-item-text">
                        ${isLink ? 
                            `<a href="${targetUrl}" target="_blank" class="resource-title-link">${escapeHtml(res.title)} <i class="fa-solid fa-up-right-from-square" style="font-size: 10px;"></i></a>` :
                            `<span class="resource-title-text" style="font-size: 13px; font-weight: 600; color: var(--text-main);">${escapeHtml(res.title)}</span>`
                        }
                        <span class="resource-meta">${escapeHtml(res.channelOrPublisher)} &bull; ${escapeHtml(res.durationOrDetails || 'Guide')}</span>
                    </div>
                </div>
                ${isLink ? 
                    `<a href="${targetUrl}" target="_blank" class="resource-link-btn">
                        <span>Open Link</span> <i class="fa-solid fa-chevron-right"></i>
                     </a>` : 
                    `<button class="resource-link-btn" onclick="startResourceChallenge('${res.resourceId}', '${escapeHtml(res.title)}', event)">
                        <span>Start</span> <i class="fa-solid fa-play"></i>
                     </button>`
                }
            `;
            
            if (res.type === 'Documentation' || res.type === 'Article') {
                docsCount++;
                docsList.appendChild(item);
            } else if (res.type === 'Practice') {
                practiceCount++;
                practiceList.appendChild(item);
            } else if (res.type === 'Project') {
                projectCount++;
                projectList.appendChild(item);
            }
        }
    });

    if (videosCount === 0) {
        videoGrid.innerHTML = '<div style="grid-column: 1/-1; text-align: center; color: var(--text-muted); padding: 20px;">No recommended videos found.</div>';
    }
    if (docsCount === 0) {
        docsList.innerHTML = '<p style="color: var(--text-muted); text-align: center; padding: 10px;">No articles or docs found.</p>';
    }
    if (practiceCount === 0) {
        practiceList.innerHTML = '<p style="color: var(--text-muted); text-align: center; padding: 10px;">No practice challenges found.</p>';
    }
    if (projectCount === 0) {
        projectList.innerHTML = '<p style="color: var(--text-muted); text-align: center; padding: 10px;">No projects found.</p>';
    }
}

function playYoutubeVideo(videoId, title) {
    const modal = document.getElementById('youtubeModal');
    const player = document.getElementById('youtubePlayer');
    const modalTitle = document.getElementById('youtubeModalTitle');

    modalTitle.innerText = title || 'Watch Tutorial';
    player.src = `https://www.youtube.com/embed/${videoId}?autoplay=1`;
    modal.style.display = 'flex';
}

function closeYoutubeModal() {
    const modal = document.getElementById('youtubeModal');
    const player = document.getElementById('youtubePlayer');

    player.src = '';
    modal.style.display = 'none';
}

function toggleResourceProgress(resourceId, event) {
    if (event) event.stopPropagation();
    
    // Find checking status
    const targetEl = document.querySelector(`[data-resource-id="${resourceId}"]`);
    const isCompleted = targetEl ? !targetEl.classList.contains('checked') : true;
    
    fetchWithAuth(`/resources/${encodeURIComponent(resourceId)}/progress`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ isCompleted })
    })
    .then(res => {
        if (!res.ok) throw new Error('Failed to update progress');
        return res.json();
    })
    .then(data => {
        const elements = document.querySelectorAll(`[data-resource-id="${resourceId}"]`);
        elements.forEach(el => {
            if (data.isCompleted) {
                el.classList.add('checked');
                if (el.classList.contains('resource-checkbox')) {
                    el.title = 'Completed';
                } else {
                    el.title = 'Marked Completed';
                    el.style.color = 'var(--success)';
                    const icon = el.querySelector('i');
                    if (icon) icon.className = 'fa-solid fa-square-check';
                }
            } else {
                el.classList.remove('checked');
                if (el.classList.contains('resource-checkbox')) {
                    el.title = 'Mark Completed';
                } else {
                    el.title = 'Mark Completed';
                    el.style.color = 'var(--text-muted)';
                    const icon = el.querySelector('i');
                    if (icon) icon.className = 'fa-regular fa-square';
                }
            }
        });
    })
    .catch(err => {
        alert(err.message);
    });
}

function startResourceChallenge(resourceId, title, event) {
    if (event) event.stopPropagation();
    
    const confirmAdd = confirm(`Practice Challenge: "${title}"\n\nWould you like to add this study task to your Study Planner?`);
    if (confirmAdd) {
        const dueDate = new Date();
        dueDate.setDate(dueDate.getDate() + 3);
        
        fetchWithAuth('/planner/add', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                taskText: `Practice: ${title}`,
                category: 'Daily Goal',
                dueDate: dueDate.toISOString()
            })
        })
        .then(res => {
            if (!res.ok) throw new Error('Failed to add task to planner');
            return res.json();
        })
        .then(() => {
            alert('Successfully added to your Study Planner goals!');
            loadPlannerTasks();
        })
        .catch(err => alert(err.message));
    }
}

function loadRoadmapNodeResources(id, skillName) {
    const nodeEl = document.getElementById(`roadmap-node-${id}`);
    if (!nodeEl) return;

    let resourcesPanel = nodeEl.querySelector(`.roadmap-resources-panel`);
    if (!resourcesPanel) {
        resourcesPanel = document.createElement('div');
        resourcesPanel.className = 'roadmap-resources-panel';
        resourcesPanel.style.marginTop = '10px';
        resourcesPanel.style.padding = '12px';
        resourcesPanel.style.borderRadius = 'var(--radius-sm)';
        resourcesPanel.style.background = 'rgba(0, 0, 0, 0.2)';
        resourcesPanel.style.borderLeft = '2px solid var(--success)';
        
        resourcesPanel.innerHTML = `
            <div class="roadmap-subpanel-title"><i class="fa-solid fa-graduation-cap"></i> Recommended Study Resources</div>
            <div class="roadmap-resources-list" style="display: flex; flex-direction: column; gap: 8px; margin-top: 8px;">
                <p style="color: var(--text-muted); font-size: 12px; margin: 0;"><i class="fa-solid fa-spinner fa-spin"></i> Finding tutorials and docs...</p>
            </div>
        `;
        
        const contentEl = nodeEl.querySelector('.roadmap-node-content');
        if (contentEl) {
            contentEl.appendChild(resourcesPanel);
        }
    } else {
        return; // Already loaded or loading
    }

    const listEl = resourcesPanel.querySelector('.roadmap-resources-list');

    Promise.all([
        fetchWithAuth(`/resources/topic?query=${encodeURIComponent(skillName)}`).then(res => {
            if (!res.ok) throw new Error('Failed to fetch resources');
            return res.json();
        }),
        fetchWithAuth('/resources/progress').then(res => {
            if (!res.ok) throw new Error('Failed to fetch progress');
            return res.json();
        })
    ])
    .then(([resources, completedIds]) => {
        const completedSet = new Set(completedIds);
        listEl.innerHTML = '';

        if (resources.length === 0) {
            listEl.innerHTML = '<p style="color: var(--text-muted); font-size: 12px; margin: 0;">No learning resources found for this skill.</p>';
            return;
        }

        // Limit to 3 resources (e.g. 1 video, 1 doc, 1 project/practice)
        const sortedResources = [];
        const video = resources.find(r => r.type === 'Video');
        if (video) sortedResources.push(video);
        const doc = resources.find(r => r.type === 'Documentation' || r.type === 'Article');
        if (doc) sortedResources.push(doc);
        const practiceOrProject = resources.find(r => r.type === 'Practice' || r.type === 'Project');
        if (practiceOrProject) sortedResources.push(practiceOrProject);

        resources.forEach(r => {
            if (sortedResources.length < 3 && !sortedResources.includes(r)) {
                sortedResources.push(r);
            }
        });

        sortedResources.forEach(res => {
            const isCompleted = completedSet.has(res.resourceId);
            const completeClass = isCompleted ? 'checked' : '';
            const checkIcon = isCompleted ? 'fa-solid fa-square-check' : 'fa-regular fa-square';
            const isVideo = res.type === 'Video';
            const isLink = res.resourceId.startsWith('http');
            const targetUrl = isLink ? res.resourceId : '#';

            const item = document.createElement('div');
            item.className = 'resource-item';
            item.style.padding = '8px 12px';
            item.style.fontSize = '12px';
            item.style.background = 'rgba(255, 255, 255, 0.01)';
            item.innerHTML = `
                <div class="resource-item-details">
                    <div class="resource-checkbox ${completeClass}" data-resource-id="${res.resourceId}" onclick="toggleResourceProgress('${res.resourceId}', event)" title="${isCompleted ? 'Completed' : 'Mark Completed'}" style="width: 16px; height: 16px;">
                        <i class="fa-solid fa-check" style="font-size: 9px;"></i>
                    </div>
                    <div class="resource-item-text">
                        ${isVideo ? 
                            `<a href="#" onclick="playYoutubeVideo('${res.resourceId}', '${escapeHtml(res.title)}'); event.preventDefault();" class="resource-title-link" style="font-size:12px;"><i class="fa-solid fa-circle-play" style="color: #ef4444; margin-right: 4px;"></i> ${escapeHtml(res.title)}</a>` :
                            (isLink ? 
                                `<a href="${targetUrl}" target="_blank" class="resource-title-link" style="font-size:12px;">${escapeHtml(res.title)} <i class="fa-solid fa-up-right-from-square" style="font-size: 8px;"></i></a>` :
                                `<span class="resource-title-text" style="font-size: 12px; font-weight: 600; color: var(--text-main);">${escapeHtml(res.title)}</span>`
                            )
                        }
                        <span class="resource-meta" style="font-size: 10px;">${escapeHtml(res.channelOrPublisher)} &bull; ${escapeHtml(res.durationOrDetails || 'Guide')}</span>
                    </div>
                </div>
                ${isLink ? 
                    `<a href="${targetUrl}" target="_blank" class="resource-link-btn" style="padding: 4px 8px; font-size: 10px;">
                        <i class="fa-solid fa-up-right-from-square"></i>
                     </a>` : 
                    (isVideo ? 
                        `<button class="resource-link-btn" onclick="playYoutubeVideo('${res.resourceId}', '${escapeHtml(res.title)}'); event.stopPropagation();" style="padding: 4px 8px; font-size: 10px;">
                            <i class="fa-solid fa-play"></i>
                         </button>` :
                        `<button class="resource-link-btn" onclick="startResourceChallenge('${res.resourceId}', '${escapeHtml(res.title)}', event)" style="padding: 4px 8px; font-size: 10px;">
                            <i class="fa-solid fa-play"></i>
                         </button>`
                    )
                }
            `;
            listEl.appendChild(item);
        });
    })
    .catch(err => {
        console.error('Error loading roadmap node resources:', err);
        listEl.innerHTML = '<p style="color: var(--danger); font-size: 12px; margin: 0;">Error loading resources.</p>';
    });
}


// ==================== CHAT WITH NOTES ====================

function startChatFromNote(noteId) {
    activeChatNoteId = noteId;
    switchView('chat');
}

function loadChatDocuments() {
    const list = document.getElementById('chatNoteList');
    list.innerHTML = '';

    fetchWithAuth('/notes')
        .then(res => res.json())
        .then(notes => {
            if (notes.length === 0) {
                list.innerHTML = '<div style="padding:16px;color:var(--text-muted);font-size:12px;">No documents available. Upload notes first!</div>';
                disableChatInput(true);
                return;
            }

            notes.forEach(note => {
                const item = document.createElement('div');
                item.className = 'chat-note-item';
                if (activeChatNoteId === note.id) item.classList.add('active');
                item.innerText = note.title;
                item.onclick = () => selectChatDocument(note.id, note.title);
                list.appendChild(item);
            });

            if (activeChatNoteId) {
                const selected = notes.find(n => n.id === activeChatNoteId);
                if (selected) {
                    selectChatDocument(selected.id, selected.title);
                } else {
                    selectFirstChatDocument(notes);
                }
            } else {
                selectFirstChatDocument(notes);
            }
        });
}

function selectFirstChatDocument(notes) {
    if (notes.length > 0) {
        selectChatDocument(notes[0].id, notes[0].title);
    }
}

function disableChatInput(isDisabled) {
    document.getElementById('chatInput').disabled = isDisabled;
    document.getElementById('btnChatSend').disabled = isDisabled;
}

function selectChatDocument(noteId, title) {
    activeChatNoteId = noteId;
    
    // Highlight item
    document.querySelectorAll('.chat-note-item').forEach(item => {
        item.classList.remove('active');
        if (item.innerText === title) item.classList.add('active');
    });

    document.getElementById('chatHeaderTitle').innerText = `Notes Chat: ${title}`;
    disableChatInput(false);

    // Load History
    const chatMessages = document.getElementById('chatMessages');
    chatMessages.innerHTML = '<div style="text-align:center;color:var(--text-muted);">Loading chat history...</div>';

    fetchWithAuth(`/chat/${noteId}/history`)
        .then(res => res.json())
        .then(messages => {
            chatMessages.innerHTML = '';
            if (messages.length === 0) {
                chatMessages.innerHTML = `
                    <div class="chat-message ai">
                        Hi! I am your AI Study Buddy. Ask me any question related to <strong>${escapeHtml(title)}</strong>, and I will search the document to explain the core concepts.
                    </div>`;
                return;
            }

            messages.forEach(msg => {
                appendChatMessage(msg.sender, msg.messageText);
            });
            scrollToBottom();
        });
}

function appendChatMessage(sender, text) {
    const chatMessages = document.getElementById('chatMessages');
    const msg = document.createElement('div');
    msg.className = `chat-message ${sender.toLowerCase()}`;
    msg.innerHTML = sender === 'AI' ? parseMarkdown(text) : escapeHtml(text);
    chatMessages.appendChild(msg);
}

function scrollToBottom() {
    const chatMessages = document.getElementById('chatMessages');
    chatMessages.scrollTop = chatMessages.scrollHeight;
}

document.getElementById('btnChatSend').addEventListener('click', sendChatMessage);
document.getElementById('chatInput').addEventListener('keypress', (e) => {
    if (e.key === 'Enter') sendChatMessage();
});

function sendChatMessage() {
    const input = document.getElementById('chatInput');
    const text = input.value.trim();
    if (!text || !activeChatNoteId) return;

    input.value = '';
    appendChatMessage('User', text);
    scrollToBottom();

    // Append AI Typing indicator
    const chatMessages = document.getElementById('chatMessages');
    const typing = document.createElement('div');
    typing.className = 'chat-message ai typing-indicator';
    typing.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Study Buddy is thinking...';
    chatMessages.appendChild(typing);
    scrollToBottom();

    fetchWithAuth(`/chat/${activeChatNoteId}/send`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ messageText: text })
    })
    .then(res => res.json())
    .then(aiMsg => {
        // Remove indicator
        typing.remove();
        appendChatMessage('AI', aiMsg.messageText);
        scrollToBottom();
    })
    .catch(err => {
        typing.remove();
        appendChatMessage('AI', `Sorry, something went wrong: ${err.message}`);
        scrollToBottom();
    });
}

// ==================== QUIZ SECTION ====================

function setupQuizListeners() {
    const selector = document.getElementById('quizNoteSelector');
    document.getElementById('btnGenerateQuiz').addEventListener('click', () => {
        const noteId = selector.value;
        if (!noteId) {
            alert('Please select a study guide first.');
            return;
        }
        generateAndStartQuiz(noteId);
    });

    document.getElementById('btnQuizQuit').addEventListener('click', () => {
        if (confirm('Quit quiz? Progress will not be saved.')) {
            resetQuizScreen();
        }
    });

    document.getElementById('btnQuizNext').addEventListener('click', () => {
        currentQuizIndex++;
        if (currentQuizIndex < currentQuiz.questions.length) {
            loadQuizQuestion(currentQuizIndex);
        } else {
            completeQuiz();
        }
    });
}

function startQuizFromNote(noteId) {
    switchView('quizzes');
    document.getElementById('quizNoteSelector').value = noteId;
    generateAndStartQuiz(noteId);
}

function loadQuizDocuments() {
    const selector = document.getElementById('quizNoteSelector');
    selector.innerHTML = '<option value="">Select a Study Note</option>';
    
    fetchWithAuth('/notes')
        .then(res => res.json())
        .then(notes => {
            notes.forEach(note => {
                const opt = document.createElement('option');
                opt.value = note.id;
                opt.innerText = note.title;
                selector.appendChild(opt);
            });
        });
}

function generateAndStartQuiz(noteId) {
    const welcome = document.getElementById('quizWelcomeScreen');
    const player = document.getElementById('quizPlayerArea');
    const qText = document.getElementById('quizQuestionText');
    const opts = document.getElementById('quizOptionsContainer');
    const nextBtn = document.getElementById('btnQuizNext');

    welcome.style.display = 'none';
    player.style.display = 'block';
    qText.innerText = 'Creating study questions from notes...';
    opts.innerHTML = '';
    nextBtn.disabled = true;

    fetchWithAuth(`/quiz/generate/${noteId}`, { method: 'POST' })
        .then(async res => {
            const data = await res.json();
            if (!res.ok) throw new Error(data.message || 'Failed to generate quiz');
            return data;
        })
        .then(data => {
            // Fetch the populated quiz structure
            return fetchWithAuth(`/quiz/${data.quizId}`);
        })
        .then(res => res.json())
        .then(quiz => {
            currentQuiz = quiz;
            currentQuizIndex = 0;
            currentQuizScore = 0;
            activeQuizAnswers = new Array(quiz.questions.length).fill(null);
            loadQuizQuestion(0);
        })
        .catch(err => {
            alert(err.message);
            resetQuizScreen();
        });
}

function loadQuizQuestion(index) {
    const q = currentQuiz.questions[index];
    document.getElementById('quizQuestionNumber').innerText = `Question ${index + 1} of ${currentQuiz.questions.length}`;
    document.getElementById('quizScoreTracker').innerText = `Score: ${currentQuizScore}/${currentQuiz.questions.length}`;
    
    document.getElementById('quizQuestionText').innerText = q.questionText;
    document.getElementById('quizExplanationArea').style.display = 'none';
    document.getElementById('btnQuizNext').disabled = true;

    const container = document.getElementById('quizOptionsContainer');
    container.innerHTML = '';

    // Check Question Type
    if (q.questionType === 'TrueFalse') {
        createOptionButton('A', q.optionA || 'True', container);
        createOptionButton('B', q.optionB || 'False', container);
    } else {
        if (q.optionA) createOptionButton('A', q.optionA, container);
        if (q.optionB) createOptionButton('B', q.optionB, container);
        if (q.optionC) createOptionButton('C', q.optionC, container);
        if (q.optionD) createOptionButton('D', q.optionD, container);
    }
}

function createOptionButton(letter, text, container) {
    const btn = document.createElement('button');
    btn.className = 'quiz-option';
    btn.innerHTML = `<strong>${letter}.</strong> ${escapeHtml(text)}`;
    btn.onclick = () => selectQuizAnswer(letter, btn);
    container.appendChild(btn);
}

function selectQuizAnswer(letter, element) {
    const q = currentQuiz.questions[currentQuizIndex];
    if (activeQuizAnswers[currentQuizIndex] !== null) return; // already answered

    activeQuizAnswers[currentQuizIndex] = letter;
    
    const isCorrect = letter.trim().toUpperCase() === q.correctAnswer.trim().toUpperCase();
    if (isCorrect) {
        element.classList.add('correct');
        currentQuizScore++;
    } else {
        element.classList.add('incorrect');
        // Highlight correct option
        document.querySelectorAll('.quiz-option').forEach(btn => {
            if (btn.innerText.startsWith(`${q.correctAnswer}.`)) {
                btn.classList.add('correct');
            }
        });
    }

    // Display Explanation
    if (q.explanation) {
        document.getElementById('quizExplanationText').innerText = q.explanation;
        document.getElementById('quizExplanationArea').style.display = 'block';
    }

    document.getElementById('quizScoreTracker').innerText = `Score: ${currentQuizScore}/${currentQuiz.questions.length}`;
    document.getElementById('btnQuizNext').disabled = false;
    
    if (currentQuizIndex === currentQuiz.questions.length - 1) {
        document.getElementById('btnQuizNext').innerHTML = 'Show Score Card <i class="fa-solid fa-chart-pie"></i>';
    } else {
        document.getElementById('btnQuizNext').innerHTML = 'Next Question <i class="fa-solid fa-arrow-right"></i>';
    }
}

function completeQuiz() {
    const scorePct = Math.round((currentQuizScore / currentQuiz.questions.length) * 100);
    
    // Save Score
    fetchWithAuth(`/quiz/${currentQuiz.id}/submit`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ score: scorePct })
    })
    .then(() => {
        // Show Scoreboard view
        document.getElementById('quizPlayerArea').style.display = 'none';
        document.getElementById('quizScoreScreen').style.display = 'block';
        document.getElementById('quizScorePercent').innerText = `${scorePct}%`;
        
        // Progress ring coloring
        const ring = document.getElementById('quizScoreCircle');
        ring.style.background = `conic-gradient(var(--success) ${scorePct}%, rgba(255,255,255,0.05) ${scorePct}%)`;

        const msg = document.getElementById('quizScoreMessage');
        if (scorePct >= 80) {
            msg.innerText = 'Superb work! You have shown a strong understanding of this content.';
        } else if (scorePct >= 50) {
            msg.innerText = 'Good attempt. A quick revision of your summary notes should help clear up gaps.';
        } else {
            msg.innerText = 'Try reviewing the summary and chatting with the document before re-taking the quiz.';
        }
    })
    .catch(err => alert(err.message));
}

function resetQuizScreen() {
    document.getElementById('quizPlayerArea').style.display = 'none';
    document.getElementById('quizScoreScreen').style.display = 'none';
    document.getElementById('quizWelcomeScreen').style.display = 'block';
    loadQuizDocuments();
}

// ==================== FLASHCARDS SECTION ====================

function startFlashcardFromNote(noteId) {
    switchView('flashcards');
    document.getElementById('flashcardNoteSelector').value = noteId;
    loadFlashcardDeck(noteId);
}

document.getElementById('btnLoadFlashcards').addEventListener('click', () => {
    const noteId = document.getElementById('flashcardNoteSelector').value;
    if (!noteId) {
        alert('Please select a study guide first.');
        return;
    }
    loadFlashcardDeck(noteId);
});

function loadFlashcardDocuments() {
    const selector = document.getElementById('flashcardNoteSelector');
    selector.innerHTML = '<option value="">Select a Study Note</option>';
    
    fetchWithAuth('/notes')
        .then(res => res.json())
        .then(notes => {
            notes.forEach(note => {
                const opt = document.createElement('option');
                opt.value = note.id;
                opt.innerText = note.title;
                selector.appendChild(opt);
            });
        });
}

function loadFlashcardDeck(noteId) {
    const selectorScreen = document.getElementById('flashcardSelectorScreen');
    const studyArea = document.getElementById('flashcardStudyArea');
    const frontText = document.getElementById('flashcardFrontText');
    const wrapper = document.getElementById('flashcardWrapper');
    const actionButtons = document.getElementById('flashcardAnswerButtons');

    selectorScreen.style.display = 'none';
    studyArea.style.display = 'flex';
    frontText.innerText = 'Loading card deck...';
    actionButtons.style.visibility = 'hidden';
    wrapper.classList.remove('flipped');

    // Fetch existing flashcards
    fetchWithAuth(`/quiz/note/${noteId}/flashcards`)
        .then(res => res.json())
        .then(cards => {
            if (cards.length === 0) {
                // Auto generate
                frontText.innerText = 'AI is extracting key terms...';
                return fetchWithAuth(`/quiz/note/${noteId}/flashcards/generate`, { method: 'POST' }).then(res => res.json());
            }
            return cards;
        })
        .then(cards => {
            currentFlashcards = cards;
            currentFlashcardIndex = 0;
            
            if (cards.length === 0) {
                alert('Could not generate vocabulary flashcards.');
                exitFlashcardDeck();
                return;
            }
            
            // Set up click flip
            wrapper.onclick = () => {
                wrapper.classList.toggle('flipped');
                if (wrapper.classList.contains('flipped')) {
                    actionButtons.style.visibility = 'visible';
                }
            };

            showFlashcard(0);
        })
        .catch(err => {
            alert(err.message);
            exitFlashcardDeck();
        });
}

function showFlashcard(index) {
    const card = currentFlashcards[index];
    const wrapper = document.getElementById('flashcardWrapper');
    const actionButtons = document.getElementById('flashcardAnswerButtons');

    wrapper.classList.remove('flipped');
    actionButtons.style.visibility = 'hidden';

    document.getElementById('flashcardProgressText').innerText = `Card ${index + 1} of ${currentFlashcards.length}`;
    document.getElementById('flashcardFrontText').innerText = card.front;
    document.getElementById('flashcardBackText').innerText = card.back;
}

function reviewFlashcard(isCorrect) {
    const card = currentFlashcards[currentFlashcardIndex];
    
    // Call review scoring API
    fetchWithAuth(`/quiz/flashcards/${card.id}/review`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ isCorrect })
    })
    .then(() => {
        currentFlashcardIndex++;
        if (currentFlashcardIndex < currentFlashcards.length) {
            showFlashcard(currentFlashcardIndex);
        } else {
            alert('Deck review complete! Spaced repetition intervals have been updated.');
            exitFlashcardDeck();
        }
    })
    .catch(err => alert(err.message));
}

function exitFlashcardDeck() {
    document.getElementById('flashcardStudyArea').style.display = 'none';
    document.getElementById('flashcardSelectorScreen').style.display = 'block';
    loadFlashcardDocuments();
}

// ==================== STUDY PLANNER SECTION ====================

function setupPlannerListeners() {
    document.getElementById('plannerForm').addEventListener('submit', (e) => {
        e.preventDefault();
        const text = document.getElementById('taskText').value;
        const category = document.getElementById('taskCategory').value;
        const date = document.getElementById('taskDate').value;

        fetchWithAuth('/planner/add', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                taskText: text,
                category: category,
                dueDate: date ? new Date(date).toISOString() : null
            })
        })
        .then(res => res.json())
        .then(() => {
            document.getElementById('taskText').value = '';
            document.getElementById('taskDate').value = '';
            loadPlannerTasks();
        })
        .catch(err => alert(err.message));
    });
}

function loadPlannerTasks() {
    const list = document.getElementById('plannerTasks');
    list.innerHTML = '<p style="text-align:center;color:var(--text-muted);">Syncing calendar...</p>';

    fetchWithAuth('/planner')
        .then(res => res.json())
        .then(tasks => {
            if (tasks.length === 0) {
                list.innerHTML = '<p style="color: var(--text-muted); font-size: 14px; text-align: center; padding: 20px;">No goals added yet. Plan some tasks above!</p>';
                return;
            }

            list.innerHTML = '';
            tasks.forEach(task => {
                const item = document.createElement('div');
                item.className = `task-item ${task.isCompleted ? 'completed' : ''}`;
                
                const catClass = task.category.toLowerCase().split(' ')[0]; // daily, weekly, exam
                const dueText = new Date(task.dueDate).toLocaleDateString();

                item.innerHTML = `
                    <div class="task-checkbox ${task.isCompleted ? 'checked' : ''}" onclick="togglePlannerTask(${task.id})">
                        <i class="fa-solid fa-check"></i>
                    </div>
                    <div class="task-text">${escapeHtml(task.taskText)} <span style="font-size:11px;color:var(--text-light);margin-left:8px;">(Due ${dueText})</span></div>
                    <span class="task-badge ${catClass}">${escapeHtml(task.category)}</span>
                    <button class="task-delete" onclick="deletePlannerTask(${task.id})" title="Delete task">
                        <i class="fa-solid fa-trash-can"></i>
                    </button>
                `;
                list.appendChild(item);
            });
        });
}

function togglePlannerTask(id) {
    fetchWithAuth(`/planner/${id}/toggle`, { method: 'POST' })
        .then(() => loadPlannerTasks())
        .catch(err => alert(err.message));
}

function deletePlannerTask(id) {
    fetchWithAuth(`/planner/${id}`, { method: 'DELETE' })
        .then(() => loadPlannerTasks())
        .catch(err => alert(err.message));
}

function loadExamCountdown() {
    const examDate = localStorage.getItem('study_buddy_exam_date');
    const examName = localStorage.getItem('study_buddy_exam_name');

    const timer = document.getElementById('examCountdownTimer');
    const label = document.getElementById('examCountdownLabel');

    if (examDate) {
        document.getElementById('settingsExamDate').value = examDate;
        document.getElementById('settingsExamName').value = examName || '';

        const target = new Date(examDate);
        const today = new Date();
        const diffTime = target - today;
        const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));

        if (diffDays > 0) {
            timer.innerText = `${diffDays}d`;
            label.innerText = `until ${examName || 'Exam'}`;
        } else if (diffDays === 0) {
            timer.innerText = 'Today!';
            label.innerText = `for ${examName || 'Exam'}`;
        } else {
            timer.innerText = 'Done';
            label.innerText = `${examName || 'Exam'} passed`;
        }
    } else {
        timer.innerText = '--';
        label.innerText = 'No exam date configured';
    }
}

function saveExamCountdown() {
    const examDate = document.getElementById('settingsExamDate').value;
    const examName = document.getElementById('settingsExamName').value;

    if (!examDate) {
        alert('Please pick a date.');
        return;
    }

    localStorage.setItem('study_buddy_exam_date', examDate);
    localStorage.setItem('study_buddy_exam_name', examName || 'Exam');
    loadExamCountdown();
    alert('Countdown calendar updated!');
}

// ==================== SETTINGS & API KEY ====================

function loadSettings() {
    const apiKey = localStorage.getItem('study_buddy_api_key') || '';
    document.getElementById('settingsOpenAiKey').value = apiKey;
}

function saveApiKey() {
    const key = document.getElementById('settingsOpenAiKey').value.trim();
    if (!key) {
        alert('Please enter a key.');
        return;
    }
    localStorage.setItem('study_buddy_api_key', key);
    alert('API Key saved locally. Custom AI completions are now enabled!');
}

function clearApiKey() {
    localStorage.removeItem('study_buddy_api_key');
    document.getElementById('settingsOpenAiKey').value = '';
    alert('API Key cleared. Reverting to local heuristic parser.');
}

// ==================== CAREER ROADMAP SECTION ====================

function setupRoadmapListeners() {
    document.getElementById('btnGenerateRoadmap').addEventListener('click', () => {
        const track = document.getElementById('roadmapGoalSelector').value;
        generateRoadmap(track);
    });
}

function generateRoadmap(careerGoal) {
    const nodesContainer = document.getElementById('roadmapNodes');
    nodesContainer.innerHTML = '<p style="text-align:center;color:var(--primary);padding:20px;"><i class="fa-solid fa-spinner fa-spin"></i> Architecting your career roadmap with AI...</p>';

    fetchWithAuth('/roadmap/generate', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ careerGoal })
    })
    .then(async res => {
        const data = await res.json();
        if (!res.ok) throw new Error(data.message || 'Failed to generate roadmap');
        return data;
    })
    .then(() => {
        loadRoadmap();
        loadRecommendations();
        alert('Your customized learning path has been generated successfully!');
    })
    .catch(err => {
        alert(err.message);
        nodesContainer.innerHTML = '<p style="color:var(--danger);text-align:center;padding:20px;">Error generating learning path.</p>';
    });
}

function loadRoadmap() {
    const nodesContainer = document.getElementById('roadmapNodes');
    const titleText = document.getElementById('roadmapTitle');

    fetchWithAuth('/roadmap')
        .then(res => res.json())
        .then(data => {
            if (!data.goal) {
                nodesContainer.innerHTML = '<p style="color: var(--text-muted); font-size: 14px; text-align: center; padding: 20px;">No career goal set yet. Select a track above to generate your learning path!</p>';
                document.getElementById('readinessScorePercent').innerText = '0%';
                document.getElementById('readinessScoreCircle').style.background = 'conic-gradient(var(--success) 0%, rgba(255,255,255,0.05) 0%)';
                return;
            }

            titleText.innerText = `Learning Roadmap for ${data.goal}`;
            
            if (data.items.length === 0) {
                nodesContainer.innerHTML = '<p style="color: var(--text-muted); font-size: 14px; text-align: center; padding: 20px;">Your roadmap is empty. Regenerate using the button above.</p>';
                return;
            }

            nodesContainer.innerHTML = '';
            
            const grouped = {};
            data.items.forEach(item => {
                const week = item.weekRange || 'General';
                if (!grouped[week]) grouped[week] = [];
                grouped[week].push(item);
            });

            const completedCount = data.items.filter(i => i.status === 'Completed').length;
            const pct = Math.round((completedCount / data.items.length) * 100);
            
            document.getElementById('readinessScorePercent').innerText = `${pct}%`;
            document.getElementById('readinessScoreCircle').style.background = `conic-gradient(var(--success) ${pct}%, rgba(255,255,255,0.05) ${pct}%)`;

            for (const week in grouped) {
                const header = document.createElement('div');
                header.className = 'roadmap-week-header';
                header.innerHTML = `<i class="fa-solid fa-calendar-week"></i> ${escapeHtml(week)}`;
                nodesContainer.appendChild(header);

                grouped[week].forEach(item => {
                    const node = document.createElement('div');
                    node.className = `roadmap-node ${item.status.toLowerCase()}`;
                    node.id = `roadmap-node-${item.id}`;
                    
                    const statusIcon = item.status === 'Completed' ? '<i class="fa-solid fa-check"></i>' : (item.status === 'InProgress' ? '<i class="fa-solid fa-spinner fa-spin"></i>' : '');
                    const diffClass = item.difficulty.toLowerCase();

                    node.innerHTML = `
                        <div class="roadmap-node-header" onclick="toggleRoadmapAccordion(${item.id})">
                            <div class="roadmap-node-status-indicator" onclick="cycleRoadmapStatus(${item.id}, event)">
                                ${statusIcon}
                            </div>
                            <div class="roadmap-node-title">${escapeHtml(item.skillName)}</div>
                            <span class="roadmap-node-difficulty ${diffClass}">${escapeHtml(item.difficulty)}</span>
                        </div>
                        <div class="roadmap-node-content">
                            <div class="roadmap-subpanel project">
                                <div class="roadmap-subpanel-title"><i class="fa-solid fa-code-fork"></i> Practice Project</div>
                                <div>${escapeHtml(item.projects)}</div>
                            </div>
                            <div class="roadmap-subpanel interview">
                                <div class="roadmap-subpanel-title"><i class="fa-solid fa-circle-question"></i> Mock Interview Prep</div>
                                <div>${escapeHtml(item.interviewQuestions)}</div>
                            </div>
                        </div>
                    `;
                    nodesContainer.appendChild(node);
                });
            }
        })
        .catch(err => {
            nodesContainer.innerHTML = `<p style="color:var(--danger);text-align:center;padding:20px;">Error loading roadmap: ${err.message}</p>`;
        });
}

function toggleRoadmapAccordion(id) {
    const node = document.getElementById(`roadmap-node-${id}`);
    if (node) {
        const isExpanding = !node.classList.contains('expanded');
        node.classList.toggle('expanded');
        
        if (isExpanding) {
            const titleEl = node.querySelector('.roadmap-node-title');
            const skillName = titleEl ? titleEl.innerText : '';
            loadRoadmapNodeResources(id, skillName);
        }
    }
}

function cycleRoadmapStatus(id, event) {
    event.stopPropagation();
    fetchWithAuth(`/roadmap/${id}/toggle`, { method: 'POST' })
        .then(() => {
            loadRoadmap();
        })
        .catch(err => alert(err.message));
}

function loadRecommendations() {
    const card = document.getElementById('recommendationAlertCard');
    const content = document.getElementById('recommendationAlertContent');

    fetchWithAuth('/roadmap/recommendations')
        .then(res => res.json())
        .then(data => {
            if (!data.recommendations || data.recommendations.length === 0) {
                card.style.display = 'none';
                return;
            }

            card.style.display = 'block';
            content.innerHTML = '';
            
            data.recommendations.forEach(rec => {
                const item = document.createElement('div');
                item.style.padding = '8px';
                item.style.background = 'rgba(245,158,11,0.05)';
                item.style.borderLeft = '2px solid var(--warning)';
                item.style.borderRadius = '4px';
                item.innerText = rec.advice;
                content.appendChild(item);
            });
        })
        .catch(err => console.error('Error loading recommendations:', err));
}

// ==================== HELPERS ====================

function escapeHtml(unsafe) {
    if (!unsafe) return "";
    return unsafe
         .replace(/&/g, "&amp;")
         .replace(/</g, "&lt;")
         .replace(/>/g, "&gt;")
         .replace(/"/g, "&quot;")
         .replace(/'/g, "&#039;");
}

function parseMarkdown(text) {
    if (!text) return "";
    let html = escapeHtml(text);
    
    // Headers
    html = html.replace(/^### (.*$)/gim, '<h3>$1</h3>');
    html = html.replace(/^## (.*$)/gim, '<h2>$1</h2>');
    html = html.replace(/^# (.*$)/gim, '<h1>$1</h1>');
    
    // Bold & Italics
    html = html.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
    html = html.replace(/\*(.*?)\*/g, '<em>$1</em>');
    
    // Lists (handling double escapes)
    html = html.replace(/^\s*-\s+(.*$)/gim, '<li>$1</li>');
    html = html.replace(/(<li>.*<\/li>)/gms, '<ul>$1</ul>');
    
    // Blockquotes
    html = html.replace(/^\s*&gt;\s+(.*$)/gim, '<blockquote>$1</blockquote>');
    
    // Line breaks
    html = html.replaceAll('\n', '<br>');
    return html;
}

// ==================== MOBILE MENU HANDLERS ====================

function setupMobileMenu() {
    const btnMenu = document.getElementById('btnMobileMenu');
    const drawer = document.getElementById('mobileDrawer');
    const btnClose = document.getElementById('btnDrawerClose');

    if (btnMenu && drawer && btnClose) {
        btnMenu.addEventListener('click', () => {
            drawer.classList.add('open');
        });

        btnClose.addEventListener('click', () => {
            drawer.classList.remove('open');
        });

        drawer.addEventListener('click', (e) => {
            if (e.target === drawer) {
                drawer.classList.remove('open');
            }
        });
    }
}

function closeDrawer() {
    const drawer = document.getElementById('mobileDrawer');
    if (drawer) {
        drawer.classList.remove('open');
    }
}
