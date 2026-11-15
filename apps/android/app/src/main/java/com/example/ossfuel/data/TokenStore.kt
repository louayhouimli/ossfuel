package com.example.ossfuel.data

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.security.GeneralSecurityException
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

/**
 * Persists the mobile access token encrypted with an Android Keystore key, plus the
 * short-lived OAuth state needed to validate the deep link that returns from GitHub.
 */
class TokenStore(context: Context) {
    private val preferences =
        context.getSharedPreferences(PREFERENCES_NAME, Context.MODE_PRIVATE)

    @Volatile
    var accessToken: String? = readToken()
        private set

    val pendingState: String?
        get() = preferences.getString(KEY_PENDING_STATE, null)

    fun saveToken(token: String) {
        accessToken = token
        preferences.edit().putString(KEY_ACCESS_TOKEN, TokenCipher.encrypt(token)).apply()
    }

    fun clearToken() {
        accessToken = null
        preferences.edit().remove(KEY_ACCESS_TOKEN).apply()
    }

    fun savePendingState(state: String) {
        preferences.edit().putString(KEY_PENDING_STATE, state).apply()
    }

    fun clearPendingState() {
        preferences.edit().remove(KEY_PENDING_STATE).apply()
    }

    private fun readToken(): String? {
        val stored = preferences.getString(KEY_ACCESS_TOKEN, null) ?: return null

        return TokenCipher.decrypt(stored)
    }

    private companion object {
        const val PREFERENCES_NAME = "ossfuel_auth"
        const val KEY_ACCESS_TOKEN = "access_token"
        const val KEY_PENDING_STATE = "pending_state"
    }
}

private object TokenCipher {
    private const val KEYSTORE = "AndroidKeyStore"
    private const val KEY_ALIAS = "ossfuel.access_token"
    private const val TRANSFORMATION = "AES/GCM/NoPadding"
    private const val IV_BYTES = 12
    private const val TAG_BITS = 128

    fun encrypt(value: String): String {
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.ENCRYPT_MODE, getOrCreateKey())

        val ciphertext = cipher.doFinal(value.toByteArray(Charsets.UTF_8))

        return Base64.encodeToString(cipher.iv + ciphertext, Base64.NO_WRAP)
    }

    fun decrypt(encoded: String): String? = try {
        val payload = Base64.decode(encoded, Base64.NO_WRAP)
        val iv = payload.copyOfRange(0, IV_BYTES)
        val ciphertext = payload.copyOfRange(IV_BYTES, payload.size)

        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.DECRYPT_MODE, getOrCreateKey(), GCMParameterSpec(TAG_BITS, iv))

        String(cipher.doFinal(ciphertext), Charsets.UTF_8)
    } catch (exception: GeneralSecurityException) {
        null
    } catch (exception: IllegalArgumentException) {
        null
    }

    private fun getOrCreateKey(): SecretKey {
        val keyStore = KeyStore.getInstance(KEYSTORE).apply { load(null) }
        (keyStore.getEntry(KEY_ALIAS, null) as? KeyStore.SecretKeyEntry)?.let {
            return it.secretKey
        }

        val generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, KEYSTORE)
        generator.init(
            KeyGenParameterSpec.Builder(
                KEY_ALIAS,
                KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT,
            )
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setKeySize(256)
                .build(),
        )

        return generator.generateKey()
    }
}
