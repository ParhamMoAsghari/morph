// Package morph is a thin Morph BaaS client stub. Full OpenAPI codegen comes later.
package morph

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"strings"
)

// Client talks to Morph Platform + Runtime APIs.
type Client struct {
	BaseURL     string
	AccessToken string
	HTTP        *http.Client
}

func (c *Client) httpClient() *http.Client {
	if c.HTTP != nil {
		return c.HTTP
	}
	return http.DefaultClient
}

// ListRecords is a placeholder for REST data CRUD.
func (c *Client) ListRecords(environmentID, entity string) (json.RawMessage, error) {
	return c.request(http.MethodGet, fmt.Sprintf("/data/v1/%s/%s", environmentID, entity), nil)
}

// RunProcess starts a published process instance.
func (c *Client) RunProcess(environmentID, slug string, body any) (json.RawMessage, error) {
	return c.request(http.MethodPost, fmt.Sprintf("/v1/environments/%s/processes/%s/run", environmentID, slug), body)
}

// InvokeFunction invokes a registered function stub.
func (c *Client) InvokeFunction(environmentID, slug string, body any) (json.RawMessage, error) {
	return c.request(http.MethodPost, fmt.Sprintf("/v1/environments/%s/functions/%s/invoke", environmentID, slug), body)
}

func (c *Client) request(method, path string, body any) (json.RawMessage, error) {
	var reader io.Reader
	if body != nil {
		b, err := json.Marshal(body)
		if err != nil {
			return nil, err
		}
		reader = bytes.NewReader(b)
	}
	req, err := http.NewRequest(method, strings.TrimRight(c.BaseURL, "/")+path, reader)
	if err != nil {
		return nil, err
	}
	req.Header.Set("Content-Type", "application/json")
	if c.AccessToken != "" {
		req.Header.Set("Authorization", "Bearer "+c.AccessToken)
	}
	resp, err := c.httpClient().Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()
	data, err := io.ReadAll(resp.Body)
	if err != nil {
		return nil, err
	}
	if resp.StatusCode < 200 || resp.StatusCode >= 300 {
		return nil, fmt.Errorf("morph: HTTP %d: %s", resp.StatusCode, string(data))
	}
	return json.RawMessage(data), nil
}
