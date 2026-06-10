<template>
  <USlideover :open="true" @update:open="v => { if (!v) $emit('close') }" side="right" title="Settings" description="Application settings" :content="{ style: 'max-width: 48rem; inset: 0; left: auto;' }">
    <template #body>
    <div class="flex flex-col flex-1 overflow-hidden">
      <!-- Tab bar + action buttons -->
      <div class="flex items-center px-6">
        <UTabs :items="settingsTabs" v-model="activeTab" :content="false" variant="link" class="flex-1" />
        <div class="flex items-center gap-2 ml-4 shrink-0">
          <UButton variant="outline" color="neutral" size="sm" @click="$emit('close')">Cancel</UButton>
          <UButton size="sm" icon="i-heroicons-document-arrow-down" :loading="saving" @click="save">{{ saving ? 'Saving…' : 'Save' }}</UButton>
        </div>
      </div>

      <!-- Body -->
      <div class="px-6 py-5 space-y-5 overflow-y-auto flex-1">
        <!-- General tab -->
        <template v-if="activeTab === 'general'">
        <div class="flex items-center justify-between">
          <div>
            <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-200">Demo Mode</h3>
            <p class="text-xs text-gray-500 dark:text-gray-300 mt-0.5">Anonymize all names for presentations</p>
          </div>
          <USwitch :model-value="demoMode.isDemoMode.value" @update:model-value="demoMode.toggle()" color="warning" />
        </div>

        <USeparator />

        <div>
          <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-200 mb-3">Security</h3>
          <div>
            <label for="apiKey" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Password</label>
            <UInput id="apiKey" name="api-key" v-model="apiKey" type="password" placeholder="Enter password…" class="w-full" />
            <p class="mt-1 text-xs text-gray-500 dark:text-gray-300">Protects all API endpoints. Encrypted at rest using Windows DPAPI with application-specific salt.</p>
          </div>
          <div class="flex items-center gap-2 mt-2">
            <span class="w-2.5 h-2.5 rounded-full" :class="store.apiKeyConfigured ? 'bg-green-500' : (store.allowUnprotectedApi ? 'bg-amber-400' : 'bg-red-400')"></span>
            <span class="text-sm" :class="store.apiKeyConfigured ? 'text-green-700 dark:text-green-400' : (store.allowUnprotectedApi ? 'text-amber-600 dark:text-amber-400' : 'text-red-600 dark:text-red-400')">
              {{ store.apiKeyConfigured ? 'Password is set' : (store.allowUnprotectedApi ? 'No password — unprotected mode is enabled' : 'No password configured') }}
            </span>
          </div>
        </div>

        <USeparator />

        <div>
          <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-200 mb-3">Azure DevOps</h3>
          <div>
            <label for="pat" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Personal Access Token</label>
            <UInput id="pat" name="pat" v-model="pat" type="password" placeholder="Enter PAT…" class="w-full" />
            <p class="mt-1 text-xs text-gray-500 dark:text-gray-300">Encrypted at rest using Windows DPAPI with application-specific salt (bound to your user account).</p>
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
                  <span class="text-xs text-gray-400 dark:text-gray-300">Not configured</span>
                </span>
              </div>
              <div class="space-y-2">
                <div>
                  <USelectMenu
                    v-model="dbForms[i - 1].driver"
                    :items="dbDriverOptions"
                    value-key="value"
                    size="sm"
                  />
                </div>
                <div class="grid grid-cols-3 gap-2">
                  <div class="col-span-2">
                    <UInput name="server" v-model="dbForms[i - 1].server" size="sm" :placeholder="'e.g. SERVER-' + i" />
                  </div>
                  <div>
                    <UInput name="port" v-model.number="dbForms[i - 1].port" type="number" size="sm" :placeholder="dbForms[i - 1].driver === 'postgres' ? '5432' : '1433'" />
                  </div>
                </div>
                <div class="grid grid-cols-2 gap-2">
                  <UInput name="username" v-model="dbForms[i - 1].username" size="sm" placeholder="Username" />
                  <UInput name="password" v-model="dbForms[i - 1].password" type="password" size="sm" placeholder="Password" />
                </div>
                <div class="flex items-center gap-2">
                  <UButton size="xs" variant="soft" :loading="testingIdx === i - 1" @click="testServer(i - 1)">
                    {{ testingIdx === i - 1 ? 'Testing…' : 'Test' }}
                  </UButton>
                  <span v-if="testResults[i - 1]" class="text-xs" :class="testSuccesses[i - 1] ? 'text-green-600' : 'text-red-600'">{{ testResults[i - 1] }}</span>
                </div>
              </div>
            </div>
          </div>
          <p class="mt-2 text-xs text-gray-500 dark:text-gray-300">Passwords encrypted at rest using Windows DPAPI with application-specific salt.</p>
        </div>
        </template>

        <!-- Email tab -->
        <template v-if="activeTab === 'email'">
        <div>
          <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-200 mb-3">Email</h3>
          <div>
            <label for="emailFrom" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">From Address</label>
            <UInput id="emailFrom" name="email-from" v-model="emailFrom" type="email" :placeholder="store.emailFrom || 'e.g. monitor@example.com'" class="w-full" />
            <p class="mt-1 text-xs text-gray-500 dark:text-gray-300">Sender address used for notification emails.</p>
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
    </div>
    </template>
  </USlideover>
</template>

<script setup>
import { ref, reactive } from 'vue'
import { useMonitorStore } from '../stores/monitor.js'
import { useDemoMode } from '../composables/useDemoMode.js'

const emit = defineEmits(['close'])
const store = useMonitorStore()
const dbDriverOptions = [
  { value: 'sqlserver', label: 'SQL Server' },
  { value: 'postgres', label: 'PostgreSQL' },
]
const demoMode = useDemoMode()
const activeTab = ref('general')
const settingsTabs = [
  { value: 'general', label: 'General', icon: 'i-heroicons-cog-6-tooth' },
  { value: 'database', label: 'Database', icon: 'i-heroicons-circle-stack' },
  { value: 'email', label: 'Email', icon: 'i-heroicons-envelope' },
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
