<template>
  <!-- Backdrop -->
  <div class="fixed inset-0 bg-black/40 z-40 flex items-center justify-center" @click.self="$emit('close')">
    <div class="bg-white dark:bg-gray-800 rounded-xl shadow-2xl w-full max-w-lg mx-4 overflow-hidden h-[80vh] max-h-[700px] flex flex-col">
      <!-- Header -->
      <div class="flex items-center justify-between px-6 py-4 border-b border-gray-100 dark:border-gray-700">
        <h2 class="text-lg font-semibold text-primary-500 dark:text-gray-100">Settings</h2>
        <button @click="$emit('close')" class="p-1 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700 text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 transition-colors">
          <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>

      <!-- Tab bar -->
      <div class="flex border-b border-gray-200 dark:border-gray-700 px-6">
        <button
          v-for="tab in settingsTabs"
          :key="tab.key"
          @click="activeTab = tab.key"
          class="px-3 py-2.5 text-sm font-medium border-b-2 transition-colors -mb-px"
          :class="activeTab === tab.key
            ? 'border-primary-500 text-primary-600 dark:text-primary-400'
            : 'border-transparent text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300 hover:border-gray-300 dark:hover:border-gray-600'"
        >
          {{ tab.label }}
        </button>
      </div>

      <!-- Body -->
      <div class="px-6 py-5 space-y-5 overflow-y-auto flex-1">
        <!-- General tab -->
        <template v-if="activeTab === 'general'">
        <!-- Demo Mode toggle -->
        <div class="flex items-center justify-between">
          <div>
            <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-200">Demo Mode</h3>
            <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">Anonymize all names for presentations</p>
          </div>
          <button
            @click="demoMode.toggle()"
            class="relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-primary-500 focus:ring-offset-2 dark:focus:ring-offset-gray-800"
            :class="demoMode.isDemoMode.value ? 'bg-amber-500' : 'bg-gray-200 dark:bg-gray-600'"
            role="switch"
            :aria-checked="demoMode.isDemoMode.value"
          >
            <span
              class="pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out"
              :class="demoMode.isDemoMode.value ? 'translate-x-5' : 'translate-x-0'"
            ></span>
          </button>
        </div>

        <hr class="border-gray-200 dark:border-gray-700" />

        <!-- Password section -->
        <div>
          <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-200 mb-3">Security</h3>
          <div>
            <label for="apiKey" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Password</label>
            <input
              id="apiKey"
              v-model="apiKey"
              type="password"
              placeholder="Enter password…"
              class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 focus:border-primary-500 outline-none transition-shadow"
            />
            <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">Protects all API endpoints. Encrypted at rest using Windows DPAPI with application-specific salt.</p>
          </div>
          <div class="flex items-center gap-2 mt-2">
            <span class="w-2.5 h-2.5 rounded-full" :class="store.apiKeyConfigured ? 'bg-green-500' : (store.allowUnprotectedApi ? 'bg-amber-400' : 'bg-red-400')"></span>
            <span class="text-sm" :class="store.apiKeyConfigured ? 'text-green-700 dark:text-green-400' : (store.allowUnprotectedApi ? 'text-amber-600 dark:text-amber-400' : 'text-red-600 dark:text-red-400')">
              {{ store.apiKeyConfigured ? 'Password is set' : (store.allowUnprotectedApi ? 'No password — unprotected mode is enabled' : 'No password configured') }}
            </span>
          </div>
        </div>

        <hr class="border-gray-200 dark:border-gray-700" />

        <!-- PAT section -->
        <div>
          <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-200 mb-3">Azure DevOps</h3>
          <div>
            <label for="pat" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Personal Access Token</label>
            <input
              id="pat"
              v-model="pat"
              type="password"
              placeholder="Enter PAT…"
              class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 focus:border-primary-500 outline-none transition-shadow"
            />
            <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">Encrypted at rest using Windows DPAPI with application-specific salt (bound to your user account).</p>
          </div>
          <div class="flex items-center gap-2 mt-2">
            <span class="w-2.5 h-2.5 rounded-full" :class="store.patConfigured ? 'bg-green-500' : 'bg-red-400'"></span>
            <span class="text-sm" :class="store.patConfigured ? 'text-green-700 dark:text-green-400' : 'text-red-600 dark:text-red-400'">
              {{ store.patConfigured ? 'PAT is configured' : 'No PAT configured' }}
            </span>
          </div>
        </div>
        </template>

        <!-- Database tab -->
        <template v-if="activeTab === 'database'">
        <!-- DB credentials section -->
        <div>
          <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-200 mb-3">Database Servers (up to 3)</h3>
          <div class="space-y-4">
            <div v-for="i in 3" :key="i" class="border border-gray-200 dark:border-gray-600 rounded-lg p-3">
              <div class="flex items-center justify-between mb-2">
                <span class="text-xs font-semibold text-gray-600 dark:text-gray-300">Server #{{ i }}</span>
                <span v-if="store.dbServers[i - 1]?.configured" class="flex items-center gap-1">
                  <span class="w-2 h-2 rounded-full bg-green-500"></span>
                  <span class="text-xs text-green-700 dark:text-green-400">{{ store.dbServers[i - 1]?.server || 'Connected' }}</span>
                </span>
                <span v-else class="flex items-center gap-1">
                  <span class="w-2 h-2 rounded-full bg-gray-300 dark:bg-gray-600"></span>
                  <span class="text-xs text-gray-400">Not configured</span>
                </span>
              </div>
              <div class="space-y-2">
                <div>
                  <SelectMenu
                    v-model="dbForms[i - 1].driver"
                    :options="dbDriverOptions"
                    size="sm"
                  />
                </div>
                <div class="grid grid-cols-3 gap-2">
                  <div class="col-span-2">
                    <input
                      v-model="dbForms[i - 1].server"
                      type="text"
                      :placeholder="'e.g. SERVER-' + i"
                      class="w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-md text-xs bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-shadow"
                    />
                  </div>
                  <div>
                    <input
                      v-model.number="dbForms[i - 1].port"
                      type="number"
                      min="1"
                      max="65535"
                      :placeholder="dbForms[i - 1].driver === 'postgres' ? '5432' : '1433'"
                      class="w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-md text-xs bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-shadow"
                    />
                  </div>
                </div>
                <div class="grid grid-cols-2 gap-2">
                  <input
                    v-model="dbForms[i - 1].username"
                    type="text"
                    placeholder="Username"
                    class="w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-md text-xs bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-shadow"
                  />
                  <input
                    v-model="dbForms[i - 1].password"
                    type="password"
                    placeholder="Password"
                    class="w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-md text-xs bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-shadow"
                  />
                </div>
                <div class="flex items-center gap-2">
                  <button
                    @click="testServer(i - 1)"
                    :disabled="testingIdx === i - 1"
                    class="px-2.5 py-1 text-xs font-medium text-indigo-700 dark:text-indigo-300 bg-indigo-50 dark:bg-indigo-900/50 border border-indigo-200 dark:border-indigo-700 rounded-md hover:bg-indigo-100 dark:hover:bg-indigo-800/50 disabled:opacity-50 transition-colors"
                  >
                    {{ testingIdx === i - 1 ? 'Testing…' : 'Test' }}
                  </button>
                  <span v-if="testResults[i - 1]" class="text-xs" :class="testSuccesses[i - 1] ? 'text-green-600' : 'text-red-600'">{{ testResults[i - 1] }}</span>
                </div>
              </div>
            </div>
          </div>
          <p class="mt-2 text-xs text-gray-500 dark:text-gray-400">Passwords encrypted at rest using Windows DPAPI with application-specific salt.</p>
        </div>
        </template>

        <!-- Email tab -->
        <template v-if="activeTab === 'email'">
        <!-- Email From section -->
        <div>
          <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-200 mb-3">Email</h3>
          <div>
            <label for="emailFrom" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">From Address</label>
            <input
              id="emailFrom"
              v-model="emailFrom"
              type="email"
              :placeholder="store.emailFrom || 'e.g. monitor@example.com'"
              class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 focus:border-primary-500 outline-none transition-shadow"
            />
            <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">Sender address used for notification emails.</p>
          </div>
          <div class="flex items-center gap-2 mt-2">
            <span class="w-2.5 h-2.5 rounded-full" :class="store.emailFromConfigured ? 'bg-green-500' : 'bg-amber-400'"></span>
            <span class="text-sm" :class="store.emailFromConfigured ? 'text-green-700 dark:text-green-400' : 'text-amber-600 dark:text-amber-400'">
              {{ store.emailFromConfigured ? `From: ${store.emailFrom}` : 'No from address configured' }}
            </span>
          </div>
        </div>
        </template>
      </div>

      <!-- Footer -->
      <div class="px-6 py-4 bg-gray-50 dark:bg-gray-800/50 border-t border-gray-100 dark:border-gray-700 flex justify-end gap-3">
        <button
          @click="$emit('close')"
          class="px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-700 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-600 transition-colors"
        >
          Cancel
        </button>
        <button
          @click="save"
          :disabled="saving"
          class="px-4 py-2 text-sm font-medium text-white bg-primary-600 rounded-lg hover:bg-primary-700 disabled:opacity-50 transition-colors"
        >
          {{ saving ? 'Saving…' : 'Save' }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive } from 'vue'
import { useMonitorStore } from '../stores/monitor.js'
import { useDemoMode } from '../composables/useDemoMode.js'
import SelectMenu from './SelectMenu.vue'

const emit = defineEmits(['close'])
const store = useMonitorStore()
const dbDriverOptions = [
  { value: 'sqlserver', label: 'SQL Server' },
  { value: 'postgres', label: 'PostgreSQL' },
]
const demoMode = useDemoMode()
const activeTab = ref('general')
const settingsTabs = [
  { key: 'general', label: 'General' },
  { key: 'database', label: 'Database' },
  { key: 'email', label: 'Email' },
]
const apiKey = ref('')
const pat = ref('')
const dbForms = reactive([
  { server: '', port: '', username: '', password: '', driver: 'sqlserver' },
  { server: '', port: '', username: '', password: '', driver: 'sqlserver' },
  { server: '', port: '', username: '', password: '', driver: 'sqlserver' },
])
const emailFrom = ref('')
const saving = ref(false)
const testingIdx = ref(-1)
const testResults = reactive(['', '', ''])
const testSuccesses = reactive([false, false, false])

function requirePort(rawPort, serverIndex) {
  if (rawPort === '' || rawPort === null || typeof rawPort === 'undefined') {
    throw new Error(`Enter a port for server #${serverIndex + 1}.`)
  }

  const port = Number(rawPort)
  if (!Number.isInteger(port) || port < 1 || port > 65535) {
    throw new Error(`Port for server #${serverIndex + 1} must be between 1 and 65535.`)
  }

  return port
}

async function testServer(idx) {
  testingIdx.value = idx
  testResults[idx] = ''
  try {
    const f = dbForms[idx]
    if (f.server || f.username || f.password) {
      const port = requirePort(f.port, idx)
      await store.saveDbCredentials(idx, f.server, port, f.username, f.password, f.driver)
    }
    const msg = await store.testDbConnection(idx)
    testResults[idx] = msg
    testSuccesses[idx] = true
  } catch (e) {
    testResults[idx] = e.message
    testSuccesses[idx] = false
  } finally {
    testingIdx.value = -1
  }
}

async function save() {
  saving.value = true
  try {
    const saveTasks = []
    const dbIndexesToReset = []

    if (apiKey.value) {
      const nextApiKey = apiKey.value
      saveTasks.push(
        store.saveApiKey(nextApiKey).then(() => {
          apiKey.value = ''
        })
      )
    }

    if (pat.value) {
      const nextPat = pat.value
      saveTasks.push(
        store.savePat(nextPat).then(() => {
          pat.value = ''
        })
      )
    }

    for (let i = 0; i < 3; i++) {
      const f = dbForms[i]
      if (f.server || f.username || f.password) {
        const port = requirePort(f.port, i)
        dbIndexesToReset.push(i)
        saveTasks.push(store.saveDbCredentials(i, f.server, port, f.username, f.password, f.driver, false))
      }
    }

    if (emailFrom.value) {
      const nextEmailFrom = emailFrom.value
      saveTasks.push(
        store.saveEmailFrom(nextEmailFrom).then(() => {
          emailFrom.value = ''
        })
      )
    }

    await Promise.all(saveTasks)

    if (dbIndexesToReset.length > 0) {
      await store.fetchDbCredentialsStatus()
      for (const i of dbIndexesToReset) {
        const f = dbForms[i]
        f.server = ''
        f.port = ''
        f.username = ''
        f.password = ''
      }
    }

    emit('close')
  } catch (e) {
    store._toast('Failed to save: ' + e.message, 'error')
  } finally {
    saving.value = false
  }
}
</script>
